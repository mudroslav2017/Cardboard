import fs from "node:fs";
import vm from "node:vm";
import assert from "node:assert/strict";

const source = fs.readFileSync("Assets/Cardboard/Plugins/WebGL/CardboardWebMouse.jslib", "utf8");
function fixture(request) {
  const handlers = new Map();
  const add = (name, fn) => handlers.set(name, [...(handlers.get(name) || []), fn]);
  const canvas = { requestPointerLock: request || (() => Promise.resolve()),
    getBoundingClientRect: () => ({width:1280,height:720}) };
  const document = { pointerLockElement: null, hidden:false, addEventListener:add };
  const context = { Module:{canvas}, document, window:{addEventListener:add},
    HEAPF32:new Float32Array(4), LibraryManager:{library:{}},
    mergeInto:(target, library) => Object.assign(target,library) };
  vm.createContext(context); vm.runInContext(source, context);
  const lib = context.LibraryManager.library;
  context.CardboardWebMouse = lib.$CardboardWebMouse;
  const emit = (name, event = {}) => (handlers.get(name) || []).forEach(fn => fn(event));
  const read = () => {const locked=lib.CardboardWebMouse_Read(0); return {locked, x:context.HEAPF32[0], y:context.HEAPF32[1]};};
  const configure = (capture=true, tracking=true) => lib.CardboardWebMouse_Configure(capture,tracking);
  const lock = () => {document.pointerLockElement=canvas;emit("pointerlockchange");configure();emit("mousemove",{movementX:0,movementY:0});};
  configure();
  return {canvas,document,context,emit,read,configure,lock};
}
let passes=0;
function check(name, fn) { fn(); passes++; process.stdout.write("PASS "+name+"\n"); }
const f = fixture();
check("An unlocked browser never supplies look input", () => {
  f.emit("mousemove",{movementX:40,movementY:20}); assert.deepEqual(f.read(),{locked:0,x:0,y:0});
});
f.lock();
check("Native movement remains relative at every screen boundary", () => {
  for(const edge of [{screenX:0,screenY:0},{screenX:1920,screenY:1080},{screenX:-5000,screenY:4000}]) {
    f.emit("mousemove",{...edge,movementX:12,movementY:-3}); assert.deepEqual(f.read(),{locked:1,x:12,y:3});
  }
});
check("Multiple input events accumulate without polling-rate scaling or repeated consumption", () => {
  for(let i=0;i<100;i++)f.emit("mousemove",{movementX:8,movementY:2});
  assert.deepEqual(f.read(),{locked:1,x:800,y:-200}); assert.deepEqual(f.read(),{locked:1,x:0,y:0});
});
check("Warp spikes are rejected per axis without discarding valid movement", () => {
  f.emit("mousemove",{movementX:-1280,movementY:4});f.emit("mousemove",{movementX:5,movementY:720});
  assert.deepEqual(f.read(),{locked:1,x:5,y:-4});
});
check("Non-finite browser samples cannot rotate the camera", () => {
  f.emit("mousemove",{movementX:NaN,movementY:Infinity});assert.deepEqual(f.read(),{locked:1,x:0,y:0});
});
check("Losing real pointer lock flushes input even if Unity still reports a lock", () => {
  f.emit("mousemove",{movementX:20,movementY:20});f.document.pointerLockElement=null;f.emit("pointerlockchange");
  assert.deepEqual(f.read(),{locked:0,x:0,y:0});
});
f.lock();
check("The first reacquisition event cannot create a recenter jump", () => {
  f.emit("pointerlockchange");f.emit("mousemove",{movementX:350,movementY:250});
  f.emit("mousemove",{movementX:6,movementY:-2});assert.deepEqual(f.read(),{locked:1,x:6,y:2});
});
check("Paused/cinematic motion is discarded, not replayed on returning to look", () => {
  f.configure(true,false);f.emit("mousemove",{movementX:100,movementY:100});assert.equal(f.read().locked,0);
  f.configure();f.emit("mousemove",{movementX:300,movementY:300});f.emit("mousemove",{movementX:9,movementY:1});
  assert.deepEqual(f.read(),{locked:1,x:9,y:-1});
});
check("Tab focus and visibility changes reset buffered motion", () => {
  f.emit("mousemove",{movementX:80,movementY:40});f.emit("blur");assert.deepEqual(f.read(),{locked:1,x:0,y:0});
  f.document.hidden=true;f.emit("visibilitychange");f.emit("mousemove",{movementX:60,movementY:20});
  assert.deepEqual(f.read(),{locked:0,x:0,y:0});f.document.hidden=false;
});
check("Opening menus disables input without requesting another lock", () => {
  f.configure(false,false);f.emit("mousemove",{movementX:80,movementY:40});assert.deepEqual(f.read(),{locked:0,x:0,y:0});
});
const calls = [];
const raw = fixture(options => {calls.push(options);return Promise.resolve();});
await raw.canvas.requestPointerLock();assert.equal(calls[0].unadjustedMovement,true);passes++;
process.stdout.write("PASS Unity lock requests prefer raw unaccelerated input\n");
const fallbackCalls=[];
const fallback=fixture(options => {
  fallbackCalls.push(options);
  return options?.unadjustedMovement ? Promise.reject(Object.assign(new Error(),{name:"NotSupportedError"})) : Promise.resolve();
});
await fallback.canvas.requestPointerLock();assert.equal(fallbackCalls.length,2);assert.equal(fallbackCalls[1],undefined);passes++;
process.stdout.write("PASS Browsers without raw input fall back to ordinary pointer lock\n");
let deniedCalls=0;
const denied=fixture(() => {deniedCalls++;return Promise.reject(Object.assign(new Error(),{name:"NotAllowedError"}));});
await assert.rejects(denied.canvas.requestPointerLock(),{name:"NotAllowedError"});assert.equal(deniedCalls,1);passes++;
process.stdout.write("PASS Denied locks are not retried outside a user gesture\n");
fs.writeFileSync("Library/FactoryWebMouseJavaScriptVerification.txt","RESULT: "+passes+" PASS, 0 FAIL\n");
process.stdout.write("RESULT: "+passes+" PASS, 0 FAIL\n");