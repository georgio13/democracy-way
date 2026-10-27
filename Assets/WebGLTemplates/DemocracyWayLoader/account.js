/* Headless Firebase accounts for the shared Unity Login screen.
 * Passwords never enter storage, save envelopes, research packets, or logs.
 * Session tokens, account email, and owner-specific recovery queues are retained locally.
 */
(() => {
  'use strict';
  const apiKey='AIzaSyBZS2LfhWldFXmhQnLzogz5KdinkxKfL4U',project='democracy-way';
  const base='https://firestore.googleapis.com/v1/projects/'+project+'/databases/(default)/documents';
  const prefix='dw-player-account-v1-',authKey=prefix+'auth',consentVersion='first-prytany-20261019';
  const state={auth:read(authKey,null),profile:null,slots:[],ready:false,resolved:false,instance:null,bridge:'PlayerAccountBridge',busy:false,status:'',error:false,email:''};
  const cloudContexts=new Map();
  function read(key,fallback){try{return JSON.parse(localStorage.getItem(key))||fallback;}catch{return fallback;}}
  function store(key,value){try{localStorage.setItem(key,JSON.stringify(value));}catch{throw new Error('LOCAL_STORAGE');}}
  function randomId(){return Array.from(crypto.getRandomValues(new Uint8Array(16)),b=>b.toString(16).padStart(2,'0')).join('');}
  function current(){return state.auth?context(state.auth):null;}
  // A cloud save is a complete state, so only the latest pending state per slot
  // is needed. Research choices have a separate immutable event queue.
  function compactSaves(queue){const latest=[];for(const item of queue){for(let i=latest.length-1;i>=0;i--)if(latest[i].slot===item.slot)latest.splice(i,1);latest.push(item);}return latest;}
  function context(auth){let c=cloudContexts.get(auth.uid);if(!c){c={auth,queue:compactSaves(read(prefix+'queue-'+auth.uid,[])),pendingProfile:read(prefix+'consent-pending-'+auth.uid,null),busy:false,timer:0};cloudContexts.set(auth.uid,c);}else c.auth=auth;return c;}
  function safeMessage(code){return ({OPERATION_NOT_ALLOWED:'Η σύνδεση με email δεν έχει ενεργοποιηθεί ακόμη στο Firebase. Χρειάζεται ενεργοποίηση Email/Password από τον διαχειριστή.',
    EMAIL_EXISTS:'Υπάρχει ήδη λογαριασμός με αυτό το email.',INVALID_LOGIN_CREDENTIALS:'Το email ή ο κωδικός δεν είναι σωστά.',INVALID_PASSWORD:'Το email ή ο κωδικός δεν είναι σωστά.',
    EMAIL_NOT_FOUND:'Το email ή ο κωδικός δεν είναι σωστά.',INVALID_EMAIL:'Έλεγξε τη μορφή του email.',WEAK_PASSWORD:'Ο κωδικός χρειάζεται τουλάχιστον 6 χαρακτήρες.',
    TOO_MANY_ATTEMPTS_TRY_LATER:'Έγιναν πολλές προσπάθειες. Δοκίμασε ξανά αργότερα.',USER_DISABLED:'Ο λογαριασμός δεν είναι ενεργός.',
    PERMISSION_DENIED:'Η σύνδεση πέτυχε, αλλά δεν υπάρχουν δικαιώματα συγχρονισμού. Χρειάζεται έλεγχος των κανόνων Firestore από τον διαχειριστή.',
    NETWORK:'Δεν υπάρχει σύνδεση με τον διακομιστή. Δοκίμασε ξανά όταν αποκατασταθεί η σύνδεση.',LOCAL_STORAGE:'Ο browser δεν επιτρέπει ασφαλή τοπική διατήρηση της προόδου.',
    PROFILE_REQUIRED:'Δεν αναγνωρίστηκαν τα στοιχεία του λογαριασμού. Επικοινώνησε με τον διαχειριστή.',
    TOKEN_EXPIRED:'Η συνεδρία έληξε. Συνδέσου ξανά.',INVALID_REFRESH_TOKEN:'Η συνεδρία έληξε. Συνδέσου ξανά.',USER_NOT_FOUND:'Η συνεδρία έληξε. Συνδέσου ξανά.',INVALID_ID_TOKEN:'Η συνεδρία έληξε. Συνδέσου ξανά.'})[String(code).split(' : ')[0]]||'Δεν ολοκληρώθηκε η ενέργεια. Δοκίμασε ξανά.';}
  async function jsonRequest(url,options){let response;try{response=await fetch(url,options);}catch{throw new Error('NETWORK');}let data={};try{data=await response.json();}catch{}
    if(!response.ok){const code=data.error?.status==='PERMISSION_DENIED'?'PERMISSION_DENIED':data.error?.message||data.error?.status||String(response.status);const e=new Error(code);e.status=response.status;throw e;}return data;}
  async function authCall(operation,body){return jsonRequest('https://identitytoolkit.googleapis.com/v1/accounts:'+operation+'?key='+apiKey,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});}
  function acceptAuth(data){const auth={uid:data.localId,idToken:data.idToken,refreshToken:data.refreshToken,expires:Date.now()+Number(data.expiresIn)*1000,email:typeof data.email==='string'?data.email:state.email};
    if(!auth.uid||!auth.idToken||!auth.refreshToken)throw new Error('INVALID_LOGIN_CREDENTIALS');state.auth=auth;store(authKey,auth);return auth;}
  async function token(c){if(c.auth.expires>Date.now()+60000)return c.auth.idToken;
    const j=await jsonRequest('https://securetoken.googleapis.com/v1/token?key='+apiKey,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:new URLSearchParams({grant_type:'refresh_token',refresh_token:c.auth.refreshToken})});
    if(j.user_id!==c.auth.uid)throw new Error('INVALID_LOGIN_CREDENTIALS');Object.assign(c.auth,{idToken:j.id_token,refreshToken:j.refresh_token,expires:Date.now()+Number(j.expires_in)*1000});
    if(state.auth?.uid===c.auth.uid)store(authKey,c.auth);return c.auth.idToken;}
  async function cloud(c,path,method='GET',fields){const idToken=await token(c);return jsonRequest(base+'/'+path,{method,headers:{'Content-Type':'application/json',Authorization:'Bearer '+idToken},...(fields?{body:JSON.stringify({fields})}:{})});}
  const field=value=>typeof value==='boolean'?{booleanValue:value}:typeof value==='number'?{integerValue:String(value)}:{stringValue:String(value)};
  const fields=object=>Object.fromEntries(Object.entries(object).map(([k,v])=>[k,field(v)]));
  const values=document=>Object.fromEntries(Object.entries(document.fields||{}).map(([k,v])=>[k,v.stringValue??v.booleanValue??Number(v.integerValue)]));
  async function loadProfile(c){let profile;
    try{profile=values(await cloud(c,'players/'+c.auth.uid));}
    catch(error){if(error.status!==404)throw error;
      // Neither account creation nor a missing profile implies research consent.
      profile={researchParticipantId:randomId(),researchConsent:false,consentVersion,consentUtc:new Date().toISOString()};
      try{await cloud(c,'players?documentId='+encodeURIComponent(c.auth.uid),'POST',fields(profile));}
      catch(createError){if(createError.status!==409)throw createError;profile=values(await cloud(c,'players/'+c.auth.uid));}}
    if(!/^[a-f0-9]{32}$/.test(profile.researchParticipantId))throw new Error('PROFILE_REQUIRED');
    if(c.pendingProfile)profile={...profile,...c.pendingProfile};
    store(prefix+'profile-'+c.auth.uid,profile);return profile;}
  function persistQueue(c){store(prefix+'queue-'+c.auth.uid,c.queue);}
  async function pump(c){if(!c||c.busy||!navigator.onLine||state.auth?.uid!==c.auth.uid)return;c.busy=true;
    try{while(c.pendingProfile&&state.auth?.uid===c.auth.uid){const preference=c.pendingProfile;await cloud(c,'players/'+c.auth.uid,'PATCH',fields(preference));if(c.pendingProfile===preference){c.pendingProfile=null;store(prefix+'consent-pending-'+c.auth.uid,null);}}
      while(c.queue.length&&state.auth?.uid===c.auth.uid){const item=c.queue[0],path='players/'+c.auth.uid+'/saves/'+item.slot;
        // Tombstones prevent old browser caches resurrecting a deleted slot.
        await cloud(c,path,'PATCH',fields({schemaVersion:1,slot:item.slot,envelope:item.action==='delete'?'':item.envelope,deleted:item.action==='delete',updatedUtc:item.updatedUtc}));
        if(c.queue[0]===item)c.queue.shift();persistQueue(c);notifyCloud(c,true,'saved');}
    }catch(error){notifyCloud(c,false,error.message);clearTimeout(c.timer);c.timer=setTimeout(()=>pump(c),15000);}finally{c.busy=false;}}
  function notifyCloud(c,ok,code){if(state.auth?.uid===c.auth.uid)send('OnCloudStatus',JSON.stringify({ok,code,pending:c.queue.length}));}
  async function loadSlots(c){const slots=await Promise.all([0,1,2,3].map(async slot=>{try{const item=values(await cloud(c,'players/'+c.auth.uid+'/saves/'+slot));return {slot,envelope:item.envelope||'',deleted:!!item.deleted,updatedUtc:item.updatedUtc||''};}
    catch(error){if(error.status===404)return {slot,envelope:'',deleted:false,updatedUtc:''};throw error;}}));
    for(const item of c.queue)slots[item.slot]={slot:item.slot,envelope:item.action==='delete'?'':item.envelope,deleted:item.action==='delete',updatedUtc:item.updatedUtc};
    store(prefix+'slots-'+c.auth.uid,slots);return slots;}
  function send(method,payload){if(state.instance)state.instance.SendMessage(state.bridge,method,payload);}
  function publish(){send('OnAccountState',JSON.stringify({busy:state.busy,status:state.status,error:state.error,email:state.email,resolved:state.resolved}));}
  function setStatus(text,error=false){state.status=text;state.error=error;publish();}
  function setBusy(value){state.busy=value;publish();}
  function researchContext(){if(window.DWResearch)window.DWResearch.setAccountContext(state.auth&&state.profile?{namespace:state.auth.uid,researchParticipantId:state.profile.researchParticipantId,allowed:state.profile.researchConsent}:null);}
  async function finishLogin(){const c=current();if(!c)throw new Error('INVALID_LOGIN_CREDENTIALS');
    state.profile=await loadProfile(c);await pump(c);state.slots=await loadSlots(c);state.ready=true;researchContext();
    const packet={uid:c.auth.uid,researchParticipantId:state.profile.researchParticipantId,researchConsent:state.profile.researchConsent,slots:state.slots};
    setStatus('Συγχρονίζονται οι αποθηκεύσεις…');send('OnAccountReady',JSON.stringify(packet));return packet;}
  async function operation(action,pending){if(state.busy)return null;setBusy(true);setStatus(pending);
    try{return await action();}catch(error){state.ready=false;setStatus(safeMessage(error.message),true);return null;}finally{setBusy(false);}}
  async function authenticate(email,password,create){return operation(async()=>{
    if(typeof email!=='string'||!/^\S+@\S+\.\S+$/.test(email.trim()))throw new Error('INVALID_EMAIL');
    if(typeof password!=='string'||password.length<6)throw new Error('WEAK_PASSWORD');
    state.email=email.trim();state.ready=false;state.profile=null;state.slots=[];researchContext();
    acceptAuth(await authCall(create?'signUp':'signInWithPassword',{email:state.email,password,returnSecureToken:true}));return finishLogin();
  },create?'Δημιουργία λογαριασμού…':'Σύνδεση με τον λογαριασμό…');}
  function queueSave(slot,envelope,action){if(!state.ready||!state.auth||!Number.isInteger(slot)||slot<0||slot>3)return false;
    if(action!=='delete'){if(typeof envelope!=='string'||envelope.length>600000)throw new Error('Invalid save size');const parsed=JSON.parse(envelope);if(parsed.slot!==slot||typeof parsed.payloadJson!=='string')throw new Error('Invalid save envelope');}
    const c=current();c.queue=c.queue.filter(item=>item.slot!==slot);c.queue.push({slot,envelope:action==='delete'?'':envelope,action,updatedUtc:new Date().toISOString()});persistQueue(c);pump(c);return true;}
  function safeQueue(slot,envelope,action){try{return queueSave(slot,envelope,action);}catch{const c=current();if(c)notifyCloud(c,false,'local-queue-unavailable');return false;}}
  async function setResearchConsent(consent){const c=current();if(!c||!state.profile)return false;const preference={...state.profile,researchConsent:consent===true,consentVersion,consentUtc:new Date().toISOString()};
    // Revocation applies immediately, even offline, and remains owner-scoped.
    state.profile=preference;researchContext();send('OnResearchConsent',consent===true?'true':'false');
    try{c.pendingProfile=preference;store(prefix+'profile-'+c.auth.uid,preference);store(prefix+'consent-pending-'+c.auth.uid,preference);await pump(c);setStatus(c.pendingProfile?'Η προτίμησή σου εφαρμόστηκε τοπικά και θα συγχρονιστεί όταν υπάρχει σύνδεση.':'Η προτίμησή σου αποθηκεύτηκε.');return true;}
    catch{setStatus('Η προτίμησή σου εφαρμόστηκε στην τρέχουσα συνεδρία. Ο browser δεν επέτρεψε τη διατήρησή της.',true);return false;}}
  function signOut(){const old=state.auth?.uid;if(old){const c=cloudContexts.get(old);if(c)clearTimeout(c.timer);cloudContexts.delete(old);}state.ready=false;state.busy=false;state.auth=null;state.profile=null;state.slots=[];state.email='';state.status='';state.error=false;try{localStorage.removeItem(authKey);}catch{}researchContext();send('OnAccountSignedOut','');publish();}
  async function resetPassword(email){return operation(async()=>{
    if(typeof email!=='string'||!/^\S+@\S+\.\S+$/.test(email.trim()))throw new Error('INVALID_EMAIL');
    try{await authCall('sendOobCode',{requestType:'PASSWORD_RESET',email:email.trim()});}catch(error){if(String(error.message).split(' : ')[0]!=='EMAIL_NOT_FOUND')throw error;}
    setStatus('Αν υπάρχει λογαριασμός, έλεγξε το email σου για οδηγίες.');return true;
  },'Αποστολή οδηγιών…');}
  async function attach(instance){state.instance=instance;state.email=typeof state.auth?.email==='string'?state.auth.email:'';publish();try{if(state.auth)return await operation(finishLogin,'Επαναφορά συνεδρίας…');return null;}finally{state.resolved=true;publish();}}
  function syncApplied(){if(state.ready)setStatus('Η σύνδεση και ο συγχρονισμός ολοκληρώθηκαν.');}
  function syncFailed(){state.ready=false;setStatus('Δεν ολοκληρώθηκε η εισαγωγή των αποθηκεύσεων. Η πρόοδος δεν διαγράφηκε. Δοκίμασε ξανά τη σύνδεση.',true);}
  window.DWAccount={attach,bridge(name){state.bridge=name;publish();},syncApplied,syncFailed,
    upload:(slot,json)=>safeQueue(slot,json,'put'),remove:slot=>safeQueue(slot,'','delete'),
    login:(email,password)=>authenticate(email,password,false),register:(email,password)=>authenticate(email,password,true),
    resetPassword,show:()=>send('OnAccountOpen',''),signOut,setResearchConsent,ready:()=>state.ready,
    context:()=>state.auth&&state.profile?{uid:state.auth.uid,researchParticipantId:state.profile.researchParticipantId,researchConsent:state.profile.researchConsent}:null};
  addEventListener('online',()=>pump(current()));
})();
