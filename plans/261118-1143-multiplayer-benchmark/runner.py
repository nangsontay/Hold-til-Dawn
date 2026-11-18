"""Linux network namespaces and cgroups; execute on host with Docker CLI."""
import argparse
import datetime as dt
import json
import math
import os
from pathlib import Path
import statistics
import subprocess as sp
import time
import uuid

here = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument("assets", type=Path, help="Directory with linux-godot, game/, and trace-{1,2,3}.jsonl")
parser.add_argument("out", type=Path)
parser.add_argument("--seconds", type=int, default=20)
parser.add_argument("--repeats", type=int, default=3)
args = parser.parse_args()
assets = args.assets.resolve()
args.out.mkdir(parents=True, exist_ok=True)

def docker(*cmd, check=True):
    return sp.run(["docker", *map(str,cmd)], check=check, text=True, stdout=sp.PIPE).stdout.strip()

def execute(container, *cmd):
    return docker("exec", container, *cmd)

def stats(values):
    s = sorted(values)
    return {f"p{p}": round(s[math.ceil(p/100*len(s))-1],2) for p in (50,95,99)}

def trial(engine, loss, rep):
    ident = uuid.uuid4().hex[:10]
    net, server, client = ["htd-"+x+"-"+ident for x in ("net","srv","cli")]
    tag = f"{engine}-loss{loss}-r{rep}"
    dest = args.out/tag
    dest.mkdir()
    peers = []
    common = ["-v",f"{assets}:/assets:ro","-v",f"{here}:/h:ro","-v",
              f"{here}/colyseus/benchmark.mjs:/bench/colyseus/benchmark.mjs:ro"]
    try:
        docker("network","create",net)
        for node in (server,client):
            docker("run","-d","--name",node,"--network",net,"--cpus","4","--memory","4g",
                   "--memory-swap","4g","--cap-add","NET_ADMIN",*common,"htd-netbench:261118","sleep","infinity")
            peers.append(node)
            execute(node,"ethtool","-K","eth0","tso","off","gso","off","gro","off")
            execute(node,"tc","qdisc","add","dev","eth0","root","netem","delay","25ms","loss",f"{loss}%")
        host = docker("inspect","-f","{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}",server)
        for i in (1,2,3):
            docker("exec","-d",server,"sh","-c",
                   f"echo $$ >/tmp/simulation{i}.pid; exec /assets/linux-godot --headless --path /assets/game "
                   f"-s /h/trace/generate-trace.gd -- {i} live 5 >/tmp/sim{i}.log 2>&1")
        for _ in range(80):
            if execute(server,"sh","-c","[ -e /dev/shm/htd-live-1.json ] && [ -e /dev/shm/htd-live-2.json ] && [ -e /dev/shm/htd-live-3.json ] && echo ok || true")=="ok": break
            time.sleep(.25)
        else: raise RuntimeError("Simulation did not start: "+execute(server,"cat","/tmp/sim1.log"))
        cmd = ("/assets/linux-godot --headless --path /h/enet --script main.gd" if engine=="enet"
               else "node /bench/colyseus/benchmark.mjs")
        for role,node in (("server",server),("client",client)):
            for i in (1,2,3):
                env = dict(BENCH_ROLE=role,BENCH_TRACE=f"/assets/trace-{i}.jsonl",BENCH_PORT=str(28000+i),
                           BENCH_CLIENTS="5", BENCH_SECONDS=str(args.seconds+45), BENCH_WARMUP="1",
                           BENCH_HOST=host if role=="client" else "0.0.0.0",BENCH_BIND="0.0.0.0")
                if role=="server": env["BENCH_LIVE"]=f"/dev/shm/htd-live-{i}.json"
                flags=[flag for k,v in env.items() for flag in ("-e",k+"="+v)]
                docker("exec","-d",*flags,node,"sh","-c",
                       f"echo $$ >/tmp/network{i}.pid; exec {cmd} >/tmp/net{i}.log 2>&1")
            marker = "ready" if role=="server" else ("joined_client" if engine=="enet" else "decoded")
            for _ in range(100):
                counts=execute(node,"sh","-c",f"for i in 1 2 3; do grep -c '{marker}' /tmp/net$i.log || true; done").splitlines()
                if len(counts)==3 and all(int(c)>= (1 if role=="server" else 5) for c in counts): break
                time.sleep(.2)
            else: raise RuntimeError(f"{role} failed: "+execute(node,"tail","-20","/tmp/net1.log"))
        time.sleep(2)
        offsets = execute(client,"sh","-c","wc -c < /tmp/net1.log; wc -c < /tmp/net2.log; wc -c < /tmp/net3.log").split()
        raw=execute(server,"node","/h/sample.mjs",args.seconds)
        (dest/"server-samples.jsonl").write_text(raw+"\n")
        samples=list(map(json.loads,raw.splitlines()))
        events=[]
        for i,off in enumerate(offsets,1):
            lines=execute(client,"tail","-c","+"+str(int(off)+1),f"/tmp/net{i}.log")
            (dest/f"client-{i}.jsonl").write_text(lines+"\n")
            for line in lines.splitlines():
                if line.startswith("{"): events.append(json.loads(line))
        first,last=samples[0],samples[-1]
        sec=(last["t"]-first["t"])/1000
        hz=int(execute(server,"getconf","CLK_TCK"))
        drops={}
        for role,node in (("client",client),("server",server)):
            stat=execute(node,"tc","-s","qdisc","show","dev","eth0")
            (dest/f"{role}-qdisc.txt").write_text(stat+"\n")
            import re
            drops[role]=int(re.search(r'dropped (\d+)',stat)[1])
        result=dict(engine=engine,loss_pct_each_way=loss,rep=rep,seconds=round(sec,2),drops=drops,
            rx_KiBs=(last["rx"]-first["rx"])/sec/1024,tx_KiBs=(last["tx"]-first["tx"])/sec/1024)
        for group in ("simulation","network"):
            result[group+"_cpu_pct"]=(last["cpu"][group]-first["cpu"][group])/hz/sec*100
            result[group+"_PSS_MiB"]=statistics.mean(s["pss"][group] for s in samples)/1024
        for key in ("rtt_ms","age_ms"):
            values=[e[key] for e in events if key in e]
            if len(values)<200: raise RuntimeError("insufficient samples: "+key)
            result[key]=stats(values)
            result[key+"_n"]=len(values)
        result={k:round(v,2) if type(v)==float else v for k,v in result.items()}
        (dest/"summary.json").write_text(json.dumps(result,indent=2)+"\n")
        print(json.dumps(result),flush=True)
    finally:
        for node in reversed(peers): docker("rm","-f",node,check=False)
        docker("network","rm",net,check=False)

for rep in range(1,args.repeats+1):
    for loss in (0,2):
        for stack in (("enet","colyseus") if (rep+loss//2)%2 else ("colyseus","enet")):
            trial(stack,loss,rep)
