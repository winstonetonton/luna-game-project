
(()=>{
"use strict";
const {Side,Kind,Game,runStep,MATCH_LIMIT_SECONDS}=window.LunaGame;
let game=null,timer=null,manualReady={[Side.P1]:true,[Side.P2]:true};
const $=id=>document.getElementById(id);
const humanSide=side=>$("ai"+side).value==="human";
const hasHuman=()=>humanSide(Side.P1)||humanSide(Side.P2);

function seedValue(){
  const value=Number($("seed").value);
  const seed=Number.isSafeInteger(value)&&value>=1&&value<=0xffffffff?value:1;
  $("seed").value=String(seed);
  return seed;
}
function randomGame(){
  const values=new Uint32Array(1);
  if(window.crypto&&window.crypto.getRandomValues)window.crypto.getRandomValues(values);
  else values[0]=Math.floor(Math.random()*0xffffffff)+1;
  $("seed").value=String(values[0]||1);
  newGame();
}

function newGame(){
  stop();
  const seed=seedValue();
  game=new Game(seed);
  manualReady={[Side.P1]:true,[Side.P2]:true};
  $("step").disabled=false;
  $("start").disabled=hasHuman();
  $("overlay").classList.add("hidden");
  $("resultTitle").textContent="";
  $("resultText").textContent="";
  $("copyStatus").textContent="";
  $("log").textContent="";
  log(`DESTINY: Tower ×${game.towerCount}`);
  render();
}
function log(s){$("log").textContent+=`[${game?game.now:0}s] ${s}\n`;$("log").scrollTop=$("log").scrollHeight;}
function step(){
  if(!game)newGame();
  if(game.winner||game.drawType)return;
  const prev=game.events.length;
  runStep(game,$("ai1").value,$("ai2").value);
  manualReady[Side.P1]=true;manualReady[Side.P2]=true;
  for(const e of game.events.slice(prev))log(`${e.event} ${JSON.stringify(e)}`);
  render();
  if(game.winner||game.drawType){
    stop();
    $("step").disabled=true;
    $("start").disabled=true;
    const result=game.winner?`P${game.winner} ${game.winType==="TIMEOUT"?"TIMEOUT ":""}VICTORY`:game.drawType.replaceAll("_"," ");
    $("resultTitle").textContent=result;
    $("resultText").textContent=`Seed ${game.seed} | P1 ${$("ai1").value} vs P2 ${$("ai2").value} | ${result} | Time ${game.now}s | Tower ${game.players[1].towers}-${game.players[2].towers} | Objectives ${game.objectiveScore(1)}-${game.objectiveScore(2)}`;
    $("overlay").classList.remove("hidden");
    $("close").focus();
  }
}
function manualDeploy(side){
  if(!game||!humanSide(side)||!manualReady[side]||game.winner||game.drawType)return;
  const kind=$("kind"+side).value;
  const pos=$("cell"+side).value.split(",").map(Number);
  if(pos.length!==2||pos.some(Number.isNaN))return;
  try{
    game.addUnit(side,kind,pos,{spend:true});
    manualReady[side]=false;
    log(`P${side} deploy ${kind} @ ${pos[0]+1}${String.fromCharCode(65+pos[1])}`);
    render();
  }catch(error){log(`P${side} deploy blocked: ${error.message}`);}
}
async function copyResult(){
  try{
    await navigator.clipboard.writeText($("resultText").textContent);
    $("copyStatus").textContent="COPIED";
  }catch{
    $("copyStatus").textContent="COPY FAILED — 結果テキストを選択してください";
  }
}
function start(){
  if(hasHuman())return;
  if(timer){stop();return;}
  step();
  if(game.winner||game.drawType)return;
  timer=setInterval(step,650);
  $("start").textContent="PAUSE";
}
function stop(){if(timer){clearInterval(timer);timer=null;}$("start").textContent="START";}
function closeResult(){
  if($("overlay").classList.contains("hidden"))return;
  $("overlay").classList.add("hidden");
  $("new").focus();
}
function render(){
  $("board").innerHTML="";
  for(let y=0;y<5;y++)for(let x=0;x<8;x++){
    const c=document.createElement("div");c.className="cell";
    const o=game.objectives.find(o=>o.pos[0]===x&&o.pos[1]===y);
    if(o){const q=document.createElement("span");q.className=`obj ${o.kind}`;q.textContent=o.kind==="tower"?"T":"O";if(o.captured)q.textContent+="✓";c.appendChild(q);}
    const u=game.unitAt([x,y]);
    if(u){const q=document.createElement("div");q.className=`unit p${u.side}`;q.textContent=u.kind;c.appendChild(q);}
    const co=document.createElement("span");co.className="coord";co.textContent=`${x+1}${String.fromCharCode(65+y)}`;c.appendChild(co);
    $("board").appendChild(c);
  }
  $("time").textContent=game.now;$("p1").textContent=game.players[1].points;$("p2").textContent=game.players[2].points;
  $("towers").textContent=game.towerCount;$("score").textContent=`${game.players[1].towers} - ${game.players[2].towers}`;
  $("objectives").textContent=`${game.objectiveScore(Side.P1)} - ${game.objectiveScore(Side.P2)}`;
  $("stall").textContent=game.stallState();
  syncHumanControls();
}
function syncHumanControls(){
  $("humanPanel").classList[hasHuman()?"remove":"add"]("hidden");
  for(const side of [Side.P1,Side.P2]){
    const row=$("human"+side),cell=$("cell"+side),button=$("deploy"+side),kind=$("kind"+side);
    row.classList[humanSide(side)?"remove":"add"]("hidden");
    if(!humanSide(side))continue;
    const previous=cell.value,cells=game.spawnCells(side);
    cell.innerHTML="";
    for(const pos of cells){
      const option=document.createElement("option");option.value=pos.join(",");option.textContent=`${pos[0]+1}${String.fromCharCode(65+pos[1])}`;cell.appendChild(option);
    }
    cell.value=cells.some(pos=>pos.join(",")===previous)?previous:(cells[0]?.join(",")||"");
    const cost=window.LunaGame.SPECS[kind.value]?.cost??Infinity;
    button.disabled=!manualReady[side]||!cells.length||game.players[side].points<cost||!!game.winner||!!game.drawType;
  }
}
window.addEventListener("DOMContentLoaded",()=>{
  $("limit").textContent=MATCH_LIMIT_SECONDS;
  $("new").onclick=newGame;$("random").onclick=randomGame;$("step").onclick=step;$("start").onclick=start;$("copy").onclick=copyResult;$("close").onclick=closeResult;
  $("ai1").onchange=newGame;$("ai2").onchange=newGame;$("kind1").onchange=syncHumanControls;$("kind2").onchange=syncHumanControls;
  $("deploy1").onclick=()=>manualDeploy(Side.P1);$("deploy2").onclick=()=>manualDeploy(Side.P2);
  newGame();
});
window.addEventListener("keydown",event=>{if(event.key==="Escape")closeResult();});
})();

