
"use strict";
const assert=require("assert");
const fs=require("fs");
const vm=require("vm");
const {Side,Kind,SPECS,Game,runStep}=require("./game_core.js");

function test(name,fn){try{fn();console.log("PASS",name)}catch(e){console.error("FAIL",name,e);process.exitCode=1}}

test("Tower count is 1-4",()=>{for(let s=1;s<=100;s++){let g=new Game(s);assert(g.towerCount>=1&&g.towerCount<=4)}});
test("Both sides have same tower count",()=>{let g=new Game(9);for(let side of [1,2])assert.equal(g.objectives.filter(o=>o.side===side&&o.kind==="tower").length,g.towerCount)});
test("Same-side tower lanes do not duplicate",()=>{let g=new Game(8);for(let side of [1,2]){let a=g.objectives.filter(o=>o.side===side&&o.kind==="tower").map(o=>o.lane);assert.equal(new Set(a).size,a.length)}});
test("Latest costs",()=>{assert.deepStrictEqual([SPECS["歩"].cost,SPECS["金"].cost,SPECS["銀"].cost,SPECS["桂馬"].cost,SPECS["香車"].cost,SPECS["弓"].cost,SPECS["角"].cost,SPECS["飛車"].cost],[1,3,3,2,2,3,4,4])});
test("Initial attack is 3 seconds later",()=>{let g=new Game(1);let u=g.addUnit(1,Kind.PAWN,[1,1],{spend:false});assert.equal(u.nextAttack,3)});
test("Lance max two and blocked",()=>{let g=new Game(1);let u=g.addUnit(1,Kind.LANCE,[1,2],{spend:false,ready:true});assert(g.legalMoves(u).some(p=>p[0]===3));g.addUnit(1,Kind.PAWN,[2,2],{spend:false,ready:true});assert(!g.legalMoves(u).some(p=>p[0]===3))});
test("Knight first-strike landing",()=>{let g=new Game(1);let k=g.addUnit(1,Kind.KNIGHT,[1,2],{spend:false,ready:true});let e=g.addUnit(2,Kind.PAWN,[3,2],{spend:false,ready:true});g.knightJump(k,e);assert(!e.alive);assert.deepStrictEqual(k.pos,[3,2])});
test("Archer kill does not advance",()=>{let g=new Game(1);let a=g.addUnit(1,Kind.ARCHER,[1,2],{spend:false,ready:true});let e=g.addUnit(2,Kind.PAWN,[2,2],{spend:false,ready:true});g.resolveAttacks([[a,e]]);assert(!e.alive);assert.deepStrictEqual(a.pos,[1,2])});
test("Melee kill advances",()=>{let g=new Game(1);let a=g.addUnit(1,Kind.ROOK,[2,2],{spend:false,ready:true});let e=g.addUnit(2,Kind.PAWN,[3,2],{spend:false,ready:true});g.resolveAttacks([[a,e]]);assert.deepStrictEqual(a.pos,[3,2])});
test("Signature ignores UID",()=>{let a=new Game(5),b=new Game(5);a.addUnit(1,Kind.PAWN,[1,2],{spend:false,ready:true});b.nextUid=50;b.addUnit(1,Kind.PAWN,[1,2],{spend:false,ready:true});assert.equal(a.normalizedSignature(),b.normalizedSignature())});
test("Captured objective timers do not prevent repetition",()=>{
  const g=new Game(5),o=g.objectives[0];
  o.captured=true;o.capSide=2;o.capStart=3;
  g.now=6;const first=g.normalizedSignature();
  g.now=60;assert.equal(g.normalizedSignature(),first);
  for(let i=0;i<4;i++)g.checkRepetition();
  assert.equal(g.drawType,"REPETITION_DRAW");
});
test("Repetition draw fires on fourth identical state",()=>{let g=new Game(5);for(let i=0;i<4;i++)g.checkRepetition();assert.equal(g.drawType,"REPETITION_DRAW")});
test("Full engine steps without exception",()=>{let g=new Game(12);for(let i=0;i<20&&!g.winner&&!g.drawType;i++)runStep(g,"rush","ranged");assert(g.now>0)});
test("Stalled games can terminate by repetition",()=>{let g=new Game(11);for(let i=0;i<500&&!g.winner&&!g.drawType;i++)runStep(g,"raid","raid");assert.equal(g.drawType,"REPETITION_DRAW")});
test("Browser scripts share a page without global declaration collisions",()=>{
  const context=vm.createContext({window:{addEventListener(){}},console,setInterval,clearInterval});
  vm.runInContext(fs.readFileSync("./game_core.js","utf8"),context,{filename:"game_core.js"});
  vm.runInContext(fs.readFileSync("./ui.js","utf8"),context,{filename:"ui.js"});
});
test("DESTINY and START controls work in a browser-like DOM",()=>{
  const elements=new Map();
  const makeElement=()=>{
    const classes=new Set();
    return {
      value:"",textContent:"",innerHTML:"",scrollTop:0,scrollHeight:0,children:[],
      classList:{add(value){classes.add(value)},remove(value){classes.delete(value)},contains(value){return classes.has(value)}},
      appendChild(child){this.children.push(child)}
    };
  };
  for(const id of ["seed","overlay","resultTitle","resultText","log","board","time","p1","p2","towers","score","stall","new","step","start","close","ai1","ai2"]){
    elements.set(id,makeElement());
  }
  elements.get("seed").value="7";elements.get("ai1").value="rush";elements.get("ai2").value="ranged";
  let ready,intervalCallback,cleared=false;
  const window={addEventListener(type,callback){if(type==="DOMContentLoaded")ready=callback}};
  const document={getElementById(id){return elements.get(id)},createElement(){return makeElement()}};
  const context=vm.createContext({window,document,console,
    setInterval(callback){intervalCallback=callback;return 1},
    clearInterval(){cleared=true}
  });
  vm.runInContext(fs.readFileSync("./game_core.js","utf8"),context,{filename:"game_core.js"});
  vm.runInContext(fs.readFileSync("./ui.js","utf8"),context,{filename:"ui.js"});
  ready();
  assert.match(elements.get("log").textContent,/DESTINY: Tower/);
  assert.equal(elements.get("board").children.length,40);
  elements.get("new").onclick();
  assert.equal(elements.get("time").textContent,0);
  elements.get("seed").value="not-a-seed";
  elements.get("new").onclick();
  assert.equal(elements.get("seed").value,"1");
  assert.match(elements.get("log").textContent,/DESTINY: Tower/);
  elements.get("start").onclick();
  assert.equal(elements.get("start").textContent,"PAUSE");
  assert.equal(elements.get("time").textContent,3);
  intervalCallback();
  assert.equal(elements.get("time").textContent,6);
  elements.get("start").onclick();
  assert.equal(elements.get("start").textContent,"START");
  assert(cleared);

  cleared=false;
  elements.get("start").onclick();
  assert.equal(elements.get("start").textContent,"PAUSE");
  elements.get("new").onclick();
  assert(cleared,"DESTINY must stop the active autoplay timer");
  assert.equal(elements.get("start").textContent,"START");
  assert.equal(elements.get("time").textContent,0);
  assert.equal((elements.get("log").textContent.match(/DESTINY:/g)||[]).length,1);

  for(let i=0;i<1000&&!elements.get("resultTitle").textContent;i++)elements.get("step").onclick();
  assert(elements.get("resultTitle").textContent,"expected the deterministic game to finish");
  assert.equal(elements.get("step").disabled,true);
  assert.equal(elements.get("start").disabled,true);
  const finalTime=elements.get("time").textContent;
  elements.get("close").onclick();
  elements.get("step").onclick();
  assert.equal(elements.get("time").textContent,finalTime);
  assert(elements.get("overlay").classList.contains("hidden"),"+3s must not reopen a completed result");
  intervalCallback=null;
  elements.get("start").onclick();
  assert.equal(intervalCallback,null,"START must not schedule a timer after the game ends");
  assert.equal(elements.get("start").textContent,"START");
  elements.get("new").onclick();
  assert.equal(elements.get("step").disabled,false);
  assert.equal(elements.get("start").disabled,false);
});

