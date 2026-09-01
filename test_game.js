
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
test("Repetition draw fires on fourth identical state",()=>{let g=new Game(5);for(let i=0;i<4;i++)g.checkRepetition();assert.equal(g.drawType,"REPETITION_DRAW")});
test("Full engine steps without exception",()=>{let g=new Game(12);for(let i=0;i<20&&!g.winner&&!g.drawType;i++)runStep(g,"rush","ranged");assert(g.now>0)});
test("Browser scripts share a page without global declaration collisions",()=>{
  const context=vm.createContext({window:{addEventListener(){}},console,setInterval,clearInterval});
  vm.runInContext(fs.readFileSync("./game_core.js","utf8"),context,{filename:"game_core.js"});
  vm.runInContext(fs.readFileSync("./ui.js","utf8"),context,{filename:"ui.js"});
});
