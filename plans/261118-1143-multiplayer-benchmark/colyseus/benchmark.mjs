import { env } from 'node:process';
import { setTimeout as delay } from 'node:timers/promises';

const host = env.BENCH_HOST ?? '127.0.0.1';
const port = Number(env.BENCH_PORT ?? 2567);
const seconds = Number(env.BENCH_SECONDS ?? 30);
const warmup = Number(env.BENCH_WARMUP ?? 2) * 1000;
const count = Number(env.BENCH_CLIENTS ?? 5);
const log = (entry) => console.log(JSON.stringify(entry));

async function server() {
  const { readFileSync } = await import('node:fs');
  const { defineRoom, defineServer, matchMaker, Room } = await import('@colyseus/core');
  const { schema, t } = await import('@colyseus/schema');
  const { WebSocketTransport } = await import('@colyseus/ws-transport');
  const frames = readFileSync(env.BENCH_TRACE, 'utf8').trim().split(/\r?\n/).map(line => JSON.parse(line));
  function validate(frame) {
    if (!Number.isInteger(frame.tick) || frame.entities.length !== 74 ||
        frame.entities.some(e => e.length !== 3 || e.some(n => !Number.isInteger(n) || n < -32768 || n > 32767))) {
      throw Error('Invalid 74-entity trace or value outside int16');
    }
    return frame;
  }
  frames.forEach(validate);
  const live = env.BENCH_LIVE;
  function liveFrame() {
    try { return validate(JSON.parse(readFileSync(live, 'utf8'))); }
    catch (error) {
      if (error.code === 'ENOENT') return frames[0];
      throw error;
    }
  }
  const Entity = schema({ x: t.int16(), y: t.int16(), hp: t.int16() }, 'Entity');
  const State = schema({ tick: t.uint32(), published: t.float64(), entities: t.array(Entity) }, 'State');
  class Wave extends Room {
    maxClients = 5;
    autoDispose = false;
    patchRate = 50;
    state = new State();
    pending = [];
    moves = 0;
    attacks = 0;
    simTick = 0;
    cursor = 0;
    onCreate() {
      for (let i = 0; i < 74; i++) this.state.entities.push(new Entity());
      this.copyFrame();
      this.onMessage('move', (_client, [seq, dx, dy]) => {
        if (Number.isSafeInteger(seq) && Math.abs(dx) <= 1 && Math.abs(dy) <= 1) this.moves++;
      });
      this.onMessage('attack', (client, [seq, target]) => {
        if (Number.isSafeInteger(seq) && target === 5) this.pending.push([client, seq, target]);
      });
      this.setFixedTimestep(() => {
        this.simTick++;
        for (const [client, seq, target] of this.pending) {
          client.send('ack', [seq, this.state.entities[target].hp, this.simTick, this.state.tick]);
          this.attacks++;
        }
        this.pending.length = 0;
      }, 60);
    }
    copyFrame() {
      const frame = live ? liveFrame() : frames[this.cursor];
      this.state.tick = frame.tick;
      this.state.published = Date.now();
      for (let i = 0; i < frame.entities.length; i++) {
        const e = this.state.entities[i];
        [e.x, e.y, e.hp] = frame.entities[i];
      }
      this.cursor = (this.cursor + 1) % frames.length;
    }
    onBeforePatch() { this.copyFrame(); }
    onJoin(client) { log({ joined: true, session: client.sessionId, room: this.roomId }); }
    onDispose() { log({ moves: this.moves, attacks: this.attacks, sim_ticks: this.simTick }); }
  }
  const app = defineServer({ transport: new WebSocketTransport(), rooms: { wave: defineRoom(Wave) }, greet: false });
  await app.listen(port, host);
  await matchMaker.createRoom('wave', {});
  log({ ready: true, port });
  await delay(seconds * 1000);
  await app.gracefullyShutdown(false);
}

async function clients() {
  const { Client } = await import('@colyseus/sdk');
  async function connect(i) {
    const room = await new Client(`ws://${host}:${port}`).join('wave');
    const pending = new Map();
    let first, lastUpdate, seq = 0, moveSeq = 0, snapshots = 0, responses = 0, previous;
    let moveTimer, firstAttack, attackTimer;
    room.onMessage('ack', ([id, hp, tick, stateTick]) => {
      if (!pending.has(id) || ![hp, tick, stateTick].every(Number.isSafeInteger)) throw Error('Invalid attack acknowledgement');
      const when = pending.get(id);
      pending.delete(id);
      responses++;
      if (when - first >= warmup) log({ rtt_ms: performance.now() - when, client: i, seq: id, sim_tick: tick, state_tick: stateTick, target_hp: hp });
    });
    function attack() {
      pending.set(++seq, performance.now());
      room.send('attack', [seq, 5]);
    }
    room.onStateChange(state => {
      const received = performance.now();
      if (state.entities.length !== 74 || !Number.isSafeInteger(state.tick) || !(state.published > 0) ||
          !Number.isInteger(state.entities[5].hp)) throw Error('Invalid decoded state');
      if (first === undefined) {
        first = lastUpdate = received;
        for (const entity of state.entities) {
          if (![entity.x, entity.y, entity.hp].every(Number.isInteger)) throw Error('Invalid decoded entity');
        }
        log({ joined: true, decoded: true, entities: state.entities.length, client: i, room: room.roomId });
        moveTimer = setInterval(() => {
          room.send('move', [++moveSeq, Math.floor((moveSeq - 1) / 20) % 2 === 0 ? 1 : -1, 0]);
        }, 50);
        firstAttack = setTimeout(() => { attack(); attackTimer = setInterval(attack, 250); }, 47 * i);
      } else if (state.tick !== previous) {
        snapshots++;
        if (received - first >= warmup) log({ age_ms: Date.now() - state.published, gap_ms: received - lastUpdate, client: i, state_tick: state.tick });
        lastUpdate = received;
      }
      previous = state.tick;
    });
    await delay(seconds * 1000);
    clearInterval(moveTimer);
    clearTimeout(firstAttack);
    clearInterval(attackTimer);
    log({ client: i, decoded_updates: snapshots, confirmed_attacks: responses });
    await room.leave();
    if (snapshots < 2 || !responses) throw Error('Client received insufficient state or attack confirmations');
  }
  if (!Number.isInteger(count) || count < 1 || count > 5) throw Error('BENCH_CLIENTS must be 1..5');
  await Promise.all(Array.from({ length: count }, (_, i) => connect(i)));
}

if (!(seconds > 0) || !(warmup >= 0)) throw Error('Invalid benchmark duration');
if (env.BENCH_ROLE === 'server') await server();
else if (env.BENCH_ROLE === 'client') await clients();
else throw Error('BENCH_ROLE must be server or client');
