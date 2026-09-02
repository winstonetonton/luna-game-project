
"use strict";

const W=8, H=5;
const START_POINTS=4, MAX_POINTS=10, MAX_UNITS=10;
const ACTION_TICK=3, CAPTURE_SECONDS=3;
const STALL_WARNING=9, STALL_REEVAL=10, REPETITION_COUNT=4;
const MATCH_LIMIT_SECONDS=180;

const Side={P1:1,P2:2};
const Kind={
  PAWN:"歩", GOLD:"金", SILVER:"銀", KNIGHT:"桂馬",
  LANCE:"香車", ARCHER:"弓", BISHOP:"角", ROOK:"飛車"
};
const SPECS={
  [Kind.PAWN]:{cost:1,hp:1,atk:1,cd:3,range:1,ranged:false},
  [Kind.GOLD]:{cost:3,hp:3,atk:1,cd:3,range:1,ranged:false},
  [Kind.SILVER]:{cost:3,hp:3,atk:1,cd:3,range:1,ranged:false},
  [Kind.KNIGHT]:{cost:2,hp:1,atk:1,cd:3,range:1,ranged:false},
  [Kind.LANCE]:{cost:2,hp:1,atk:1,cd:3,range:1,ranged:false},
  [Kind.ARCHER]:{cost:3,hp:2,atk:1,cd:6,range:2,ranged:true},
  [Kind.BISHOP]:{cost:4,hp:2,atk:1,cd:3,range:1,ranged:false},
  [Kind.ROOK]:{cost:4,hp:2,atk:2,cd:3,range:1,ranged:false},
};

function fwd(side){ return side===Side.P1?1:-1; }
function enemy(side){ return side===Side.P1?Side.P2:Side.P1; }
function inBounds([x,y]){ return x>=0&&x<W&&y>=0&&y<H; }
function keyPos([x,y]){ return `${x},${y}`; }

class RNG{
  constructor(seed=1){ this.s=(seed>>>0)||1; }
  next(){ this.s=(1664525*this.s+1013904223)>>>0; return this.s/4294967296; }
  int(n){ return Math.floor(this.next()*n); }
  sample(arr,n){
    const a=[...arr];
    for(let i=a.length-1;i>0;i--){ const j=this.int(i+1); [a[i],a[j]]=[a[j],a[i]]; }
    return a.slice(0,n);
  }
}

class Game{
  constructor(seed=1){
    this.rng=new RNG(seed);
    this.seed=seed;
    this.now=0;
    this.towerCount=1+this.rng.int(4);
    this.players={
      [Side.P1]:{points:START_POINTS,towers:0},
      [Side.P2]:{points:START_POINTS,towers:0}
    };
    this.units=[];
    this.nextUid=1;
    this.objectives=[];
    this.lastProgress=0;
    this.winner=null;
    this.winType=null;
    this.drawType=null;
    this.events=[];
    this.repetition=new Map();
    this._destiny();
  }
  log(event,data={}){ this.events.push({t:this.now,event,...data}); }
  _destiny(){
    const p1T=this.rng.sample([0,1,2,3,4],this.towerCount);
    const p2T=this.rng.sample([0,1,2,3,4],this.towerCount);
    for(let lane=0;lane<5;lane++){
      this.objectives.push({side:Side.P1,lane,kind:p1T.includes(lane)?"tower":"outpost",pos:[0,lane],captured:false,capSide:null,capStart:null});
      this.objectives.push({side:Side.P2,lane,kind:p2T.includes(lane)?"tower":"outpost",pos:[7,lane],captured:false,capSide:null,capStart:null});
    }
    this.log("destiny",{towerCount:this.towerCount,p1T,p2T});
  }
  living(side=null){ return this.units.filter(u=>u.alive&&(side===null||u.side===side)); }
  unitAt(pos){ return this.living().find(u=>u.pos[0]===pos[0]&&u.pos[1]===pos[1])||null; }
  spawnCells(side){
    const cols=side===Side.P1?[0,1,2,3]:[4,5,6,7], out=[];
    for(const x of cols)for(let y=0;y<5;y++)if(!this.unitAt([x,y]))out.push([x,y]);
    return out;
  }
  addUnit(side,kind,pos,{spend=true,ready=false}={}){
    const s=SPECS[kind];
    if(this.unitAt(pos))throw Error("occupied");
    if(this.living(side).length>=MAX_UNITS)throw Error("cap");
    if(spend){
      if(this.players[side].points<s.cost)throw Error("points");
      this.players[side].points-=s.cost;
    }
    const u={uid:this.nextUid++,side,kind,pos:[...pos],hp:s.hp,nextAction:ready?this.now:this.now+3,nextAttack:ready?this.now:this.now+3,alive:true,locked:false};
    this.units.push(u); this.log("spawn",{uid:u.uid,side,kind,pos:[...pos]});
    return u;
  }
  tickPoints(){
    if(this.now>0&&this.now%3===0){
      for(const s of [Side.P1,Side.P2])this.players[s].points=Math.min(MAX_POINTS,this.players[s].points+1);
    }
  }
  ray(u,dirs,maxd){
    const out=[];
    for(const [dx,dy] of dirs){
      for(let d=1;d<=maxd;d++){
        const p=[u.pos[0]+dx*d,u.pos[1]+dy*d];
        if(!inBounds(p))break;
        if(this.unitAt(p))break;
        out.push(p);
      }
    }
    return out;
  }
  legalMoves(u){
    const [x,y]=u.pos, f=fwd(u.side); let c=[];
    if([Kind.PAWN,Kind.SILVER,Kind.ARCHER].includes(u.kind))c=[[x+f,y]];
    else if(u.kind===Kind.GOLD)c=[[-1,-1],[-1,0],[-1,1],[0,-1],[0,1],[1,-1],[1,0],[1,1]].map(([dx,dy])=>[x+dx,y+dy]);
    else if(u.kind===Kind.KNIGHT)c=[[x+2*f,y]];
    else if(u.kind===Kind.LANCE){
      c=[];
      for(let d=1;d<=2;d++){
        const p=[x+d*f,y];
        if(!inBounds(p)||this.unitAt(p))break;
        c.push(p);
      }
    } else if(u.kind===Kind.BISHOP)c=this.ray(u,[[1,1],[1,-1],[-1,1],[-1,-1]],5);
    else if(u.kind===Kind.ROOK)c=this.ray(u,[[1,0],[-1,0],[0,1],[0,-1]],5);
    return c.filter(p=>inBounds(p)&&!this.unitAt(p));
  }
  attackables(u){
    const [x,y]=u.pos,f=fwd(u.side),out=[];
    for(const e of this.living()){
      if(e.side===u.side)continue;
      const dx=e.pos[0]-x,dy=e.pos[1]-y;
      let ok=false;
      if([Kind.PAWN,Kind.SILVER].includes(u.kind))ok=dx===f&&dy===0;
      else if(u.kind===Kind.GOLD)ok=Math.max(Math.abs(dx),Math.abs(dy))===1;
      else if(u.kind===Kind.KNIGHT)ok=false; // knight attacks only by first-strike landing
      else if(u.kind===Kind.LANCE)ok=dx===f&&Math.abs(dy)<=1;
      else if(u.kind===Kind.ARCHER)ok=(Math.abs(dx)+Math.abs(dy)>0)&&(Math.abs(dx)+Math.abs(dy)<=2);
      else if(u.kind===Kind.BISHOP)ok=Math.abs(dx)===1&&Math.abs(dy)===1;
      else if(u.kind===Kind.ROOK)ok=(Math.abs(dx)===1&&dy===0)||(Math.abs(dy)===1&&dx===0);
      if(ok)out.push(e);
    }
    return out;
  }
  knightLandingTarget(u){
    if(u.kind!==Kind.KNIGHT)return null;
    const p=[u.pos[0]+2*fwd(u.side),u.pos[1]];
    if(!inBounds(p))return null;
    const e=this.unitAt(p);
    return e&&e.side!==u.side?e:null;
  }
  target(u,enemies){
    if(!enemies.length)return null;
    const ownThreat=e=>u.side===Side.P1?e.pos[0]:7-e.pos[0];
    const dist=e=>Math.abs(e.pos[0]-u.pos[0])+Math.abs(e.pos[1]-u.pos[1]);
    return [...enemies].sort((a,b)=>{
      const ka=[a.hp/SPECS[a.kind].hp,ownThreat(a),dist(a),a.uid];
      const kb=[b.hp/SPECS[b.kind].hp,ownThreat(b),dist(b),b.uid];
      for(let i=0;i<ka.length;i++)if(ka[i]!==kb[i])return ka[i]-kb[i];
      return 0;
    })[0];
  }
  knightJump(u,target){
    if(!u.alive||u.kind!==Kind.KNIGHT)return;
    const land=[u.pos[0]+2*fwd(u.side),u.pos[1]];
    if(!inBounds(land))return;
    const occ=this.unitAt(land);
    if(occ&&occ.side===u.side)return;
    if(occ&&occ.side!==u.side){
      occ.hp-=SPECS[u.kind].atk; this.lastProgress=this.now;
      if(occ.hp<=0){occ.alive=false;u.pos=land;this.log("knight_kill",{uid:u.uid,target:occ.uid,pos:[...land]});}
      return;
    }
    u.pos=land;
  }
  resolveAttacks(pairs){
    const pos=new Map(this.units.map(u=>[u.uid,[...u.pos]])),dmg=new Map(),valid=[];
    for(const [a,t] of pairs){
      if(a.alive&&t.alive){
        dmg.set(t.uid,(dmg.get(t.uid)||0)+SPECS[a.kind].atk);valid.push([a,t]);
      }
    }
    const dead=new Set();
    for(const [uid,d] of dmg){
      const t=this.units.find(u=>u.uid===uid);t.hp-=d;this.lastProgress=this.now;
      if(t.hp<=0)dead.add(uid);
    }
    for(const uid of dead)this.units.find(u=>u.uid===uid).alive=false;
    const claims=new Map();
    for(const [a,t] of valid){
      if(a.alive&&dead.has(t.uid)&&!SPECS[a.kind].ranged){
        const k=keyPos(pos.get(t.uid)); if(!claims.has(k))claims.set(k,[]); claims.get(k).push(a);
      }
    }
    for(const [k,arr] of claims){
      const [x,y]=k.split(",").map(Number);
      if(!this.unitAt([x,y])){ const win=arr.sort((a,b)=>a.uid-b.uid)[0]; win.pos=[x,y]; }
    }
  }
  startCapture(u){
    if(!u.alive||u.locked)return;
    const o=this.objectives.find(o=>o.pos[0]===u.pos[0]&&o.pos[1]===u.pos[1]&&!o.captured&&o.side!==u.side);
    if(!o)return;
    if(o.capSide!==u.side){o.capSide=u.side;o.capStart=this.now;}
  }
  updateCaptures(){
    const newly=[];
    for(const o of this.objectives){
      if(o.captured||o.capStart===null)continue;
      const h=this.unitAt(o.pos);
      if(!h||!h.alive||h.side!==o.capSide){o.capSide=null;o.capStart=null;continue;}
      if(this.now-o.capStart>=CAPTURE_SECONDS){
        o.captured=true;h.locked=true;newly.push({o,h});this.lastProgress=this.now;
      }
    }
    for(const {o,h} of newly)if(o.kind==="tower")this.players[h.side].towers++;
    const p1=this.players[Side.P1].towers>=this.towerCount,p2=this.players[Side.P2].towers>=this.towerCount;
    if(p1&&p2)this.drawType="SIMULTANEOUS_TOWER_DRAW";
    else if(p1)this.winner=Side.P1; else if(p2)this.winner=Side.P2;
  }
  objectiveScore(side){return this.objectives.filter(o=>o.captured&&o.capSide===side).length;}
  checkTimeout(){
    if(this.now<MATCH_LIMIT_SECONDS)return false;
    const p1=this.objectiveScore(Side.P1),p2=this.objectiveScore(Side.P2);
    if(p1>p2){this.winner=Side.P1;this.winType="TIMEOUT";}
    else if(p2>p1){this.winner=Side.P2;this.winType="TIMEOUT";}
    else this.drawType="TIMEOUT_DRAW";
    this.log("timeout",{p1Objectives:p1,p2Objectives:p2});
    return true;
  }
  stallState(){
    const d=this.now-this.lastProgress;
    return d>=STALL_REEVAL?"REEVALUATE":d>=STALL_WARNING?"WARNING":"NORMAL";
  }
  normalizedSignature(){
    const units=this.living().map(u=>[
      u.side,u.kind,u.pos[0],u.pos[1],u.hp,u.locked?1:0,
      Math.max(0,u.nextAction-this.now),Math.max(0,u.nextAttack-this.now)
    ]).sort((a,b)=>JSON.stringify(a).localeCompare(JSON.stringify(b)));
    const players=[[1,this.players[1].points,this.players[1].towers],[2,this.players[2].points,this.players[2].towers]];
    const objectives=this.objectives.map(o=>[
      o.side,o.lane,o.kind,o.captured?1:0,
      o.captured?0:(o.capSide||0),
      o.captured||o.capStart===null?-1:Math.max(0,this.now-o.capStart)
    ]).sort((a,b)=>JSON.stringify(a).localeCompare(JSON.stringify(b)));
    return JSON.stringify([this.towerCount,players,objectives,units]);
  }
  checkRepetition(){
    const s=this.normalizedSignature(),n=(this.repetition.get(s)||0)+1;this.repetition.set(s,n);
    if(n>=REPETITION_COUNT)this.drawType="REPETITION_DRAW";
  }
}

function laneLoad(g,side,lane){ return g.living(side).filter(u=>u.pos[1]===lane&&!u.locked).length; }
function targetLane(g,side,ai,phase=0){
  const e=enemy(side);
  const objs=g.objectives.filter(o=>o.side===e&&!o.captured);
  const towers=objs.filter(o=>o.kind==="tower");
  const pool=(ai==="raid"&&towers.length)?towers:objs;
  if(!pool.length)return 2;
  const vals=pool.map(o=>({lane:o.lane,score:laneLoad(g,side,o.lane)*3+Math.abs(o.lane-2)+(o.kind==="tower"?-3:0)}));
  vals.sort((a,b)=>a.score-b.score||((a.lane+phase)%5)-((b.lane+phase)%5));
  return vals[0].lane;
}
function defenseLane(g,side){
  const threats=g.living(enemy(side)).map(u=>({lane:u.pos[1],progress:side===Side.P1?7-u.pos[0]:u.pos[0]}));
  if(!threats.length)return null;
  threats.sort((a,b)=>b.progress-a.progress);
  return threats[0].lane;
}
function rosterChoice(g,side,ai,phase){
  const pts=g.players[side].points,active=g.living(side).length;
  let pref;
  if(ai==="rush"){pref=[Kind.LANCE,Kind.KNIGHT,Kind.PAWN];if(active>=6&&pts<2)return null;}
  else if(ai==="ranged"){if(pts<3&&!g.living(side).some(u=>[Kind.ARCHER,Kind.GOLD].includes(u.kind)))return null;pref=phase%4===0?[Kind.GOLD,Kind.ARCHER,Kind.PAWN]:[Kind.ARCHER,Kind.PAWN,Kind.GOLD];if(active>=5&&pts<3)return null;}
  else if(ai==="raid"){pref=phase%4===0?[Kind.GOLD,Kind.KNIGHT,Kind.LANCE,Kind.PAWN]:[Kind.KNIGHT,Kind.LANCE,Kind.PAWN,Kind.GOLD];if(active>=5&&pts<2)return null;}
  else {if(pts<3&&!g.living(side).some(u=>[Kind.ARCHER,Kind.GOLD].includes(u.kind)))return null;pref=phase%3===0?[Kind.GOLD,Kind.ARCHER,Kind.PAWN]:[Kind.ARCHER,Kind.GOLD,Kind.PAWN];if(active>=5&&pts<3)return null;}
  return pref.find(k=>pts>=SPECS[k].cost)||null;
}
function planDeploy(g,side,ai,phase){
  if(g.living(side).length>=MAX_UNITS)return null;
  const kind=rosterChoice(g,side,ai,phase);if(!kind)return null;
  let lane=ai==="defense"?(defenseLane(g,side)??targetLane(g,side,ai,phase)):targetLane(g,side,ai,phase);
  if(ai==="defense"&&g.living(side).filter(u=>u.pos[1]===lane&&!u.locked).length>=2)lane=targetLane(g,side,"rush",phase);
  const cells=g.spawnCells(side),edge=side===Side.P1?3:4;
  cells.sort((a,b)=>{
    const ka=[g.living(side).some(u=>u.locked&&u.pos[1]===a[1])?1:0,Math.abs(a[1]-lane),laneLoad(g,side,a[1]),Math.abs(a[0]-edge),a[1],a[0]];
    const kb=[g.living(side).some(u=>u.locked&&u.pos[1]===b[1])?1:0,Math.abs(b[1]-lane),laneLoad(g,side,b[1]),Math.abs(b[0]-edge),b[1],b[0]];
    for(let i=0;i<ka.length;i++)if(ka[i]!==kb[i])return ka[i]-kb[i];return 0;
  });
  return cells.length?{kind,pos:cells[0]}:null;
}
function moveScore(g,u,p,ai,phase){
  const progress=(p[0]-u.pos[0])*fwd(u.side),lane=targetLane(g,u.side,ai,phase);
  let s=progress*8-Math.abs(p[1]-lane)*4-laneLoad(g,u.side,p[1])*2;
  if(ai==="rush")s+=progress*7;
  if(ai==="raid")s+=progress*3;
  if(ai==="defense")s-=Math.max(0,progress)*2;
  if(ai==="ranged"&&u.kind===Kind.ARCHER)s-=Math.max(0,progress)*4;
  return s;
}
function chooseAction(g,u,ai,phase){
  if(!u.alive||u.locked)return ["idle",null];
  if(u.kind===Kind.KNIGHT&&g.now>=u.nextAction){
    const p=[u.pos[0]+2*fwd(u.side),u.pos[1]],occ=g.unitAt(p);
    if(inBounds(p)&&(!occ||occ.side!==u.side))return ["knight",occ||p];
  }
  const enemies=g.attackables(u);
  if(enemies.length&&g.now>=u.nextAttack)return ["attack",g.target(u,enemies)];
  if(g.now<u.nextAction)return ["idle",null];
  const moves=g.legalMoves(u);if(!moves.length)return ["idle",null];
  moves.sort((a,b)=>moveScore(g,u,b,ai,phase)-moveScore(g,u,a,ai,phase));
  return ["move",moves[0]];
}

function runStep(g,ai1="rush",ai2="ranged"){
  if(g.winner||g.drawType)return;
  g.now+=3;g.tickPoints();g.updateCaptures();if(g.winner||g.drawType)return;
  if(g.checkTimeout())return;
  const phase=Math.floor(g.now/3)+g.seed;
  const plans=[[Side.P1,planDeploy(g,Side.P1,ai1,phase)],[Side.P2,planDeploy(g,Side.P2,ai2,phase)]];
  for(const [s,p] of plans)if(p)g.addUnit(s,p.kind,p.pos,{spend:true});
  const acts=g.living().map(u=>[u,chooseAction(g,u,u.side===Side.P1?ai1:ai2,phase)]);
  // Knight first strike
  for(const [u,[a,arg]] of acts)if(a==="knight"&&u.alive){g.knightJump(u,arg&&arg.uid?arg:null);u.nextAction=g.now+3;}
  // Ordinary attacks
  const pairs=[];
  for(const [u,[a,arg]] of acts)if(a==="attack"&&u.alive&&arg&&arg.alive){pairs.push([u,arg]);u.nextAttack=g.now+SPECS[u.kind].cd;u.nextAction=g.now+3;}
  if(pairs.length)g.resolveAttacks(pairs);
  // Move reservation
  const claims=new Map();
  for(const [u,[a,arg]] of acts)if(a==="move"&&u.alive&&arg){const k=keyPos(arg);if(!claims.has(k))claims.set(k,[]);claims.get(k).push(u);}
  for(const [k,arr] of claims){
    const [x,y]=k.split(",").map(Number);
    if(!g.unitAt([x,y])){arr.sort((a,b)=>a.uid-b.uid);arr[0].pos=[x,y];arr[0].nextAction=g.now+3;g.lastProgress=g.now;}
  }
  for(const u of g.living())g.startCapture(u);
  g.updateCaptures();
  if(!g.winner&&!g.drawType)g.checkRepetition();
}

const LunaGame={W,H,MATCH_LIMIT_SECONDS,Side,Kind,SPECS,Game,runStep,planDeploy,chooseAction};
if(typeof module!=="undefined")module.exports=LunaGame;
if(typeof window!=="undefined")window.LunaGame=LunaGame;

