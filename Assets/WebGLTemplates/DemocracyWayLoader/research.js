/* Anonymous, append-only Firestore transport. Schema matches the existing v5 rules.
 * Local gameplay remains available offline; queued events are retried on reconnect.
 * This is event collection, not a claim of completed research or learning efficacy.
 */
(() => {
  'use strict';
  const apiKey='AIzaSyBZS2LfhWldFXmhQnLzogz5KdinkxKfL4U',project='democracy-way';
  const prefix='dw-research-release-20261018-',queueKey=prefix+'queue',authKey=prefix+'auth';
  let queue=read(queueKey,[]),auth=read(authKey,null),busy=false,serial=Promise.resolve(),retry=0;
  const status={pending:queue.length,sent:0,lastError:null};
  function read(k,fallback){try{return JSON.parse(localStorage.getItem(k))||fallback;}catch{return fallback;}}
  function store(k,v){localStorage.setItem(k,JSON.stringify(v));}
  function persist(){status.pending=queue.length;try{store(queueKey,queue);}catch(e){status.lastError='local-storage-unavailable';console.error('[Research] Pending events could not be persisted',e);}}
  async function hash(text){const bytes=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(text));return Array.from(new Uint8Array(bytes)).map(v=>v.toString(16).padStart(2,'0')).join('').slice(0,32);}
  async function token(){
    if(auth&&auth.expires>Date.now()+60000)return auth;
    let response;
    if(auth?.refreshToken){response=await fetch('https://securetoken.googleapis.com/v1/token?key='+apiKey,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:new URLSearchParams({grant_type:'refresh_token',refresh_token:auth.refreshToken})});if(response.ok){const j=await response.json();auth={uid:j.user_id,idToken:j.id_token,refreshToken:j.refresh_token,expires:Date.now()+Number(j.expires_in)*1000};store(authKey,auth);return auth;}}
    response=await fetch('https://identitytoolkit.googleapis.com/v1/accounts:signUp?key='+apiKey,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({returnSecureToken:true})});
    if(!response.ok)throw new Error('anonymous-auth-'+response.status);
    const j=await response.json();auth={uid:j.localId,idToken:j.idToken,refreshToken:j.refreshToken,expires:Date.now()+Number(j.expiresIn)*1000};store(authKey,auth);return auth;
  }
  function fields(event){return Object.fromEntries(Object.entries(event).map(([k,v])=>[k,typeof v==='number'?(Number.isInteger(v)?{integerValue:String(v)}:{doubleValue:v}):{stringValue:String(v)}]));}
  async function pump(){
    if(busy||!queue.length||!navigator.onLine)return;busy=true;
    try{
      const identity=await token();
      while(queue.length){const event=queue[0];const url='https://firestore.googleapis.com/v1/projects/'+project+'/databases/(default)/documents/participants/'+identity.uid+'/events?documentId='+event.eventId;
        const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json','Authorization':'Bearer '+identity.idToken},body:JSON.stringify({fields:fields(event)})});
        if(!r.ok&&r.status!==409)throw new Error('firestore-'+r.status);
        queue.shift();status.sent++;status.lastError=null;retry=0;persist();
      }
    }catch(e){status.lastError=e.message;console.warn('[Research] Events remain queued:',e.message);clearTimeout(retry);retry=setTimeout(pump,15000);}
    finally{busy=false;}
  }
  async function capture(json){
    const p=JSON.parse(json);if(!/^[a-f0-9]{32}$/.test(p.runId))return;
    const ledgerKey=prefix+p.runId,ledger=read(ledgerKey,{sequence:0,signatures:{}}),metrics=p.metrics||{},campaign=p.campaign||{},bank=p.caseContent?JSON.parse(p.caseContent).cases:[];
    const base={schemaVersion:5,contentVersion:'5.0.0',t:new Date().toISOString(),session:p.runId,type:'approved_ui_checkpoint',chapterId:p.phase||'',nodeId:'',choiceId:'',outcome:'release-20261018',prytany:p.prytany,week:p.week,answer:-1,correct:-1,duration:0,treasury:p.cityFunds,personalTreasury:p.personalFunds,integrity:metrics.ethics||0,trust:metrics.consistency||0,participation:(campaign.cases||[]).filter(c=>c.completed&&c.vote!==2).length,abstentions:(campaign.cases||[]).filter(c=>c.completed&&c.vote===2).length,consistency:metrics.consistency||0,ethics:metrics.ethics||0,authority:metrics.authority||0,persuasion:metrics.persuasion||0,parrhesia:metrics.parrhesia||0,recognition:metrics.recognition||0,orientation:'unclassified',stance:'',sourceRef:'Οδός Δημοκρατίας · release 2026-10-18'};
    async function event(key,signature,change){
      if(ledger.signatures[key]===signature)return;
      const eventId=await hash(p.runId+':'+(++ledger.sequence));queue.push({...base,...change,eventId});ledger.signatures[key]=signature;
    }
    await event('checkpoint',[p.phase,p.prytany,p.week,p.biographyIndex,p.prologueIndex].join(':'),{});
    for(const c of p.choices||[]){await event('choice:'+c.stepId,JSON.stringify(c),{type:'choice',nodeId:c.stepId,choiceId:c.choiceId,stance:c.choiceId,orientation:c.institutionalLean==='democratic'?'democracy':c.institutionalLean==='oligarchic'?'oligarchy':'mixed'});}
    for(const c of campaign.cases||[]){if(c.knowledge<0)continue;const question=bank.find(b=>b.id===c.caseId);if(!question)continue;
      await event('knowledge:'+c.turn,String(c.knowledge),{type:'case_knowledge',nodeId:c.caseId,answer:c.knowledge,correct:c.knowledge===question.correct?1:0,sourceRef:question.source});
    }
    const quiz=[0,1,2,5,6,7,8,9];for(const kind of ['initial','final'])for(let i=0;i<8;i++){const answer=(campaign[kind+'Answers']||[])[i];if(answer==null||answer<0||!bank.length)continue;await event(kind+':'+i,String(answer),{type:kind+'_knowledge',nodeId:'question-'+i,answer,correct:answer===(i===0?1:bank[quiz[i]].correct)?1:0,sourceRef:i===0?'Αθηναίων Πολιτεία 43.2':bank[quiz[i]].source});}
    for(let i=0;i<2;i++){const answer=(campaign.survey||[])[i];if(answer>0)await event('survey:'+i,String(answer),{type:'experience_rating',nodeId:'experience-'+i,answer});}
    // The queue is durable before marking its entries as captured.
    persist();try{store(ledgerKey,ledger);}catch(e){console.error('[Research] Replay ledger unavailable',e);}
    pump();
  }
  window.DWResearch={capture(json){return serial=serial.then(()=>capture(json)).catch(e=>{status.lastError=e.message;console.error('[Research] Capture failed',e);});},status};
  addEventListener('online',pump);pump();
})();
