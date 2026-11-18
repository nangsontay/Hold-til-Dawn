import fs from "node:fs";
import {setTimeout as delay} from "node:timers/promises";
const read = p => fs.readFileSync(p,"utf8").trim();
const metric = name => Number(read("/sys/class/net/eth0/statistics/" + name));
function snap() {
  const cpu = {simulation:0, network:0}, pss = {simulation:0, network:0};
  for (const group of Object.keys(cpu)) {
    for (let room=1; room<=3; room++) {
      const pid=read("/tmp/" + group + room + ".pid"), root="/proc/"+pid+"/";
      const stat=read(root+"stat").split(") ")[1].split(" ");
      cpu[group]+=Number(stat[11])+Number(stat[12]);
      pss[group]+=Number(read(root+"smaps_rollup").match(/^Pss:\s+(\d+)/m)[1]);
    }
  }
  return {t:Date.now(),cpu,pss,rx:metric("rx_bytes"),tx:metric("tx_bytes")};
}
for(let i=0;i<=Number(process.argv[2]);i++) {
  console.log(JSON.stringify(snap()));
  if(i<Number(process.argv[2])) await delay(1000);
}
