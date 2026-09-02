
(()=>{
"use strict";
const {Side,Kind,Game,runStep}=window.LunaGame;
let game=null,timer=null;
const $=id=>document.getElementById(id);

function seedValue(){
  const value=Number($("seed").value);
  const seed=Number.isSafeInteger(value)&&value>=1&&value<=0xffffffff?value:1;
  $("seed").value=String(seed);
  return seed;
}

function newGame(){
  stop();
  const seed=seedValue();
  game=new Game(seed);
  $("step").disabled=false;
  $("start").disabled=false;
  $("overlay").classList.add("hidden");
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
  for(const e of game.events.slice(prev))log(`${e.event} ${JSON.stringify(e)}`);
  render();
  if(game.winner||game.drawType){
    stop();
    $("step").disabled=true;
    $("start").disabled=true;
    $("resultTitle").textContent=game.winner?`P${game.winner} VICTORY`:game.drawType.replaceAll("_"," ");
    $("resultText").textContent=`TIME ${game.now}s / Tower ${game.players[1].towers}-${game.players[2].towers}`;
    $("overlay").classList.remove("hidden");
  }
}
function start(){
  if(timer){stop();return;}
  step();
  if(game.winner||game.drawType)return;
  timer=setInterval(step,650);
  $("start").textContent="PAUSE";
}
function stop(){if(timer){clearInterval(timer);timer=null;}$("start").textContent="START";}
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
  $("stall").textContent=game.stallState();
}
window.addEventListener("DOMContentLoaded",()=>{
  $("new").onclick=newGame;$("step").onclick=step;$("start").onclick=start;$("close").onclick=()=> $("overlay").classList.add("hidden");
  newGame();
});
})();

