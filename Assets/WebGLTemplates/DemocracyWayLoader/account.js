/* Firebase email/password accounts and owner-only save synchronisation.
 * No password, email or auth token is ever placed in research packets.
 * Account refresh tokens are persisted locally to support the requested login session.
 */
(() => {
  'use strict';
  const apiKey='AIzaSyBZS2LfhWldFXmhQnLzogz5KdinkxKfL4U',project='democracy-way';
  const base='https://firestore.googleapis.com/v1/projects/'+project+'/databases/(default)/documents';
  const prefix='dw-player-account-v1-',authKey=prefix+'auth',consentVersion='first-prytany-20261019';
  const state={auth:read(authKey,null),profile:null,slots:[],ready:false,instance:null,bridge:'PlayerAccountBridge',mode:'login',busy:false,profileRequired:false};
  const cloudContexts=new Map();
  let overlay,form,message,profileMode=false;
  function read(key,fallback){try{return JSON.parse(localStorage.getItem(key))||fallback;}catch{return fallback;}}
  function store(key,value){localStorage.setItem(key,JSON.stringify(value));}
  function randomId(){return Array.from(crypto.getRandomValues(new Uint8Array(16)),b=>b.toString(16).padStart(2,'0')).join('');}
  function current(){return state.auth?context(state.auth):null;}
  function context(auth){let c=cloudContexts.get(auth.uid);if(!c){c={auth,queue:read(prefix+'queue-'+auth.uid,[]),pendingProfile:read(prefix+'consent-pending-'+auth.uid,null),busy:false,timer:0};cloudContexts.set(auth.uid,c);}else c.auth=auth;return c;}
  function safeMessage(code){return ({OPERATION_NOT_ALLOWED:'Η σύνδεση με email δεν έχει ενεργοποιηθεί ακόμη στο Firebase. Χρειάζεται ενεργοποίηση Email/Password από τον διαχειριστή.',
    EMAIL_EXISTS:'Υπάρχει ήδη λογαριασμός με αυτό το email.',INVALID_LOGIN_CREDENTIALS:'Το email ή ο κωδικός δεν είναι σωστά.',INVALID_PASSWORD:'Το email ή ο κωδικός δεν είναι σωστά.',
    EMAIL_NOT_FOUND:'Το email ή ο κωδικός δεν είναι σωστά.',INVALID_EMAIL:'Έλεγξε τη μορφή του email.',WEAK_PASSWORD:'Ο κωδικός χρειάζεται τουλάχιστον 6 χαρακτήρες.',
    TOO_MANY_ATTEMPTS_TRY_LATER:'Έγιναν πολλές προσπάθειες. Δοκίμασε ξανά αργότερα.',USER_DISABLED:'Ο λογαριασμός δεν είναι ενεργός.',
    PERMISSION_DENIED:'Η σύνδεση πέτυχε, αλλά δεν υπάρχουν ακόμη δικαιώματα συγχρονισμού. Χρειάζεται δημοσίευση των κανόνων Firestore από τον διαχειριστή.',
    NETWORK:'Δεν υπάρχει σύνδεση με τον διακομιστή. Δοκίμασε ξανά όταν αποκατασταθεί η σύνδεση.',LOCAL_STORAGE:'Ο browser δεν επιτρέπει ασφαλή τοπική διατήρηση της προόδου.',
    PROFILE_REQUIRED:'Συμπλήρωσε την ενημέρωση λογαριασμού και έρευνας.'})[String(code).split(' : ')[0]]||'Δεν ολοκληρώθηκε η ενέργεια. Δοκίμασε ξανά.';}
  async function jsonRequest(url,options){let response;try{response=await fetch(url,options);}catch{throw new Error('NETWORK');}let data={};try{data=await response.json();}catch{}
    if(!response.ok){const code=data.error?.status==='PERMISSION_DENIED'?'PERMISSION_DENIED':data.error?.message||data.error?.status||String(response.status);const e=new Error(code);e.status=response.status;throw e;}return data;}
  async function authCall(operation,body){return jsonRequest('https://identitytoolkit.googleapis.com/v1/accounts:'+operation+'?key='+apiKey,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});}
  function acceptAuth(data){const auth={uid:data.localId,idToken:data.idToken,refreshToken:data.refreshToken,expires:Date.now()+Number(data.expiresIn)*1000};
    if(!auth.uid||!auth.idToken||!auth.refreshToken)throw new Error('INVALID_LOGIN_CREDENTIALS');state.auth=auth;store(authKey,auth);return auth;}
  async function token(c){if(c.auth.expires>Date.now()+60000)return c.auth.idToken;
    const j=await jsonRequest('https://securetoken.googleapis.com/v1/token?key='+apiKey,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:new URLSearchParams({grant_type:'refresh_token',refresh_token:c.auth.refreshToken})});
    if(j.user_id!==c.auth.uid)throw new Error('INVALID_LOGIN_CREDENTIALS');Object.assign(c.auth,{idToken:j.id_token,refreshToken:j.refresh_token,expires:Date.now()+Number(j.expires_in)*1000});
    if(state.auth?.uid===c.auth.uid)store(authKey,c.auth);return c.auth.idToken;}
  async function cloud(c,path,method='GET',fields){const idToken=await token(c);return jsonRequest(base+'/'+path,{method,headers:{'Content-Type':'application/json',Authorization:'Bearer '+idToken},...(fields?{body:JSON.stringify({fields})}:{})});}
  const field=(value)=>typeof value==='boolean'?{booleanValue:value}:typeof value==='number'?{integerValue:String(value)}:{stringValue:String(value)};
  const fields=(object)=>Object.fromEntries(Object.entries(object).map(([k,v])=>[k,field(v)]));
  const values=(document)=>Object.fromEntries(Object.entries(document.fields||{}).map(([k,v])=>[k,v.stringValue??v.booleanValue??Number(v.integerValue)]));
  async function loadProfile(c,registration){
    let profile;
    try{profile=values(await cloud(c,'players/'+c.auth.uid));}
    catch(error){if(error.status!==404)throw error;if(!registration)throw new Error('PROFILE_REQUIRED');
      profile={researchParticipantId:randomId(),researchConsent:!!registration.researchConsent,consentVersion,consentUtc:new Date().toISOString()};
      try{await cloud(c,'players?documentId='+encodeURIComponent(c.auth.uid),'POST',fields(profile));}
      catch(createError){if(createError.status!==409)throw createError;profile=values(await cloud(c,'players/'+c.auth.uid));}}
    if(!/^[a-f0-9]{32}$/.test(profile.researchParticipantId))throw new Error('PROFILE_REQUIRED');
    if(c.pendingProfile)profile={...profile,...c.pendingProfile};
    store(prefix+'profile-'+c.auth.uid,profile);return profile;
  }
  function persistQueue(c){store(prefix+'queue-'+c.auth.uid,c.queue);}
  async function pump(c){if(!c||c.busy||!navigator.onLine||state.auth?.uid!==c.auth.uid)return;c.busy=true;
    try{while(c.pendingProfile&&state.auth?.uid===c.auth.uid){const preference=c.pendingProfile;await cloud(c,'players/'+c.auth.uid,'PATCH',fields(preference));if(c.pendingProfile===preference){c.pendingProfile=null;store(prefix+'consent-pending-'+c.auth.uid,null);}}
      while(c.queue.length&&state.auth?.uid===c.auth.uid){const item=c.queue[0];const path='players/'+c.auth.uid+'/saves/'+item.slot;
      // A tombstone keeps a deletion from being resurrected by an old browser cache.
      await cloud(c,path,'PATCH',fields({schemaVersion:1,slot:item.slot,envelope:item.action==='delete'?'':item.envelope,deleted:item.action==='delete',updatedUtc:item.updatedUtc}));
      // An edit made while the request was in flight remains queued as a new item.
      if(c.queue[0]===item)c.queue.shift();persistQueue(c);notifyCloud(c,true,'saved');}
    }catch(error){notifyCloud(c,false,error.message);clearTimeout(c.timer);c.timer=setTimeout(()=>pump(c),15000);}finally{c.busy=false;}}
  function notifyCloud(c,ok,code){if(state.auth?.uid===c.auth.uid)send('OnCloudStatus',JSON.stringify({ok,code,pending:c.queue.length}));}
  async function loadSlots(c){const slots=await Promise.all([0,1,2,3].map(async slot=>{try{const item=values(await cloud(c,'players/'+c.auth.uid+'/saves/'+slot));return {slot,envelope:item.envelope||'',deleted:!!item.deleted,updatedUtc:item.updatedUtc||''};}
    catch(error){if(error.status===404)return {slot,envelope:'',deleted:false,updatedUtc:''};throw error;}}));
    // Pending local edits/tombstones take precedence over the downloaded old copy.
    for(const item of c.queue)slots[item.slot]={slot:item.slot,envelope:item.action==='delete'?'':item.envelope,deleted:item.action==='delete',updatedUtc:item.updatedUtc};
    store(prefix+'slots-'+c.auth.uid,slots);return slots;}
  function send(method,payload){if(state.instance)state.instance.SendMessage(state.bridge,method,payload);}
  function researchContext(){if(window.DWResearch)window.DWResearch.setAccountContext(state.auth&&state.profile?{namespace:state.auth.uid,researchParticipantId:state.profile.researchParticipantId,allowed:state.profile.researchConsent}:null);}
  async function finishLogin(registration){const c=current();if(!c)throw new Error('INVALID_LOGIN_CREDENTIALS');
    state.profile=await loadProfile(c,registration);await pump(c);state.slots=await loadSlots(c);state.ready=true;researchContext();
    send('OnAccountReady',JSON.stringify({uid:c.auth.uid,researchParticipantId:state.profile.researchParticipantId,researchConsent:state.profile.researchConsent,slots:state.slots}));
    showStatus('Συγχρονίζονται οι αποθηκεύσεις…');return {uid:c.auth.uid,researchParticipantId:state.profile.researchParticipantId,researchConsent:state.profile.researchConsent,slots:state.slots};}
  async function authenticate(email,password,registration=null){acceptAuth(await authCall(registration?'signUp':'signInWithPassword',{email,password,returnSecureToken:true}));return finishLogin(registration);}
  function queueSave(slot,envelope,action){if(!state.ready||!state.auth||!Number.isInteger(slot)||slot<0||slot>3)return false;
    if(action!=='delete'){if(typeof envelope!=='string'||envelope.length>600000)throw new Error('Invalid save size');const parsed=JSON.parse(envelope);if(parsed.slot!==slot||typeof parsed.payloadJson!=='string')throw new Error('Invalid save envelope');}
    const c=current();c.queue.push({slot,envelope:action==='delete'?'':envelope,action,updatedUtc:new Date().toISOString()});persistQueue(c);pump(c);return true;}
  function safeQueue(slot,envelope,action){try{return queueSave(slot,envelope,action);}catch{const c=current();if(c)notifyCloud(c,false,'local-queue-unavailable');return false;}}
  function showStatus(text){if(message)message.textContent=text;}
  function setBusy(busy){state.busy=busy;if(form)for(const el of form.querySelectorAll('button,input'))el.disabled=busy;}
  function mode(next){state.mode=next;profileMode=false;const register=next==='register',reset=next==='reset';
    document.getElementById('account-email').required=true;
    document.getElementById('account-register-fields').hidden=!register;document.getElementById('account-password-label').hidden=reset;
    document.getElementById('account-password').required=!reset;document.getElementById('account-password').autocomplete=register?'new-password':'current-password';
    document.getElementById('account-privacy').required=register;document.getElementById('account-title').textContent=reset?'Επαναφορά κωδικού':register?'Δημιουργία λογαριασμού':'Είσοδος στην πόλη';
    document.getElementById('account-submit').textContent=reset?'Αποστολή συνδέσμου':register?'Δημιουργία λογαριασμού':'Σύνδεση';
    for(const el of document.querySelectorAll('[data-account-mode]'))el.setAttribute('aria-selected',String(el.dataset.accountMode===next));showStatus('');}
  function requireProfile(){mode('register');state.profileRequired=true;document.getElementById('account-email').readOnly=true;document.getElementById('account-email').required=false;document.getElementById('account-password-label').hidden=true;document.getElementById('account-password').required=false;document.getElementById('account-title').textContent='Ολοκλήρωση ενημέρωσης λογαριασμού';document.getElementById('account-submit').textContent='Αποθήκευση και είσοδος';showStatus(safeMessage('PROFILE_REQUIRED'));}
  function mount(){if(typeof document==='undefined'||overlay)return;
    overlay=document.createElement('section');overlay.id='account-overlay';overlay.hidden=true;overlay.setAttribute('aria-label','Λογαριασμός παιχνιδιού');
    overlay.innerHTML='<div class="account-panel"><img class="account-logo" src="LoaderAssets/Logo.png" alt="Οδός Δημοκρατίας"><h1 id="account-title">Είσοδος στην πόλη</h1><div class="account-tabs"><button type="button" data-account-mode="login">Σύνδεση</button><button type="button" data-account-mode="register">Εγγραφή</button></div><form id="account-form"><label>Email<input id="account-email" type="email" autocomplete="email" required></label><label id="account-password-label">Κωδικός<input id="account-password" type="password" minlength="6" autocomplete="current-password" required></label><div id="account-register-fields" hidden><label class="account-check"><input id="account-privacy" type="checkbox"><span>Διάβασα την ενημέρωση για τον λογαριασμό και την αποθήκευση προόδου.</span></label><label class="account-check"><input id="account-research" type="checkbox"><span>Συναινώ ρητά στη χρήση των απαντήσεων και των πολιτικών επιλογών μου για την περιγραφόμενη έρευνα. Η επιλογή είναι προαιρετική.</span></label></div><button id="account-submit" type="submit">Σύνδεση</button><button class="account-link" id="account-reset" type="button">Ξέχασα τον κωδικό</button></form><div id="account-profile" hidden><p>Η πρόοδος αποθηκεύεται ιδιωτικά στον λογαριασμό σου.</p><label class="account-check"><input id="account-profile-research" type="checkbox"><span>Συμμετοχή στην έρευνα με ψευδωνυμοποιημένα δεδομένα.</span></label><div class="account-actions"><button id="account-back" type="button">Επιστροφή</button><button id="account-signout" type="button">Αποσύνδεση</button></div></div><p id="account-message" role="status" aria-live="polite"></p><details><summary>Ενημέρωση λογαριασμού και έρευνας</summary><p>Το email χρησιμοποιείται για τη σύνδεση και την επαναφορά κωδικού μέσω Firebase Authentication. Οι τέσσερις αποθηκεύσεις είναι ιδιωτικές για τον λογαριασμό σου.</p><p>Αν συναινέσεις στην έρευνα, καταγράφονται απαντήσεις γνώσεων, επιλογές, μεταβολές δεικτών, πρόοδος και ενεργός χρόνος με διαφορετικό τυχαίο ερευνητικό κωδικό. Δεν στέλνονται email ή ονόματα στα ερευνητικά συμβάντα. Τα δεδομένα είναι ψευδωνυμοποιημένα, όχι απολύτως ανώνυμα. Οι επιλογές εντός παιχνιδιού δεν αποδεικνύουν την πραγματική πολιτική ιδεολογία σου.</p><p>Η άρνηση ή η μελλοντική διακοπή συμμετοχής δεν εμποδίζει το παιχνίδι. Για ενημέρωση ή αίτημα διαγραφής: <a href="mailto:giorgoschatziparaskevas@gmail.com">giorgoschatziparaskevas@gmail.com</a>. Προτεινόμενη διατήρηση ερευνητικών δεδομένων: 90 ημέρες· ο ερευνητής οφείλει να εφαρμόζει τη δηλωμένη πολιτική.</p></details><p class="account-footnote">Ο κωδικός πρόσβασης δεν αποθηκεύεται από το παιχνίδι. Ο browser διατηρεί το διακριτικό συνεδρίας για την επόμενη είσοδο.</p></div>';
    document.body.appendChild(overlay);form=document.getElementById('account-form');message=document.getElementById('account-message');
    for(const el of document.querySelectorAll('[data-account-mode]'))el.addEventListener('click',()=>{state.profileRequired=false;document.getElementById('account-email').readOnly=false;mode(el.dataset.accountMode);});
    document.getElementById('account-reset').addEventListener('click',()=>mode('reset'));
    form.addEventListener('submit',async event=>{event.preventDefault();if(state.busy)return;setBusy(true);showStatus('Παρακαλώ περίμενε…');
      const email=document.getElementById('account-email').value.trim(),password=document.getElementById('account-password').value;
      try{if(state.mode==='reset'){await authCall('sendOobCode',{requestType:'PASSWORD_RESET',email});showStatus('Αν υπάρχει λογαριασμός, έλεγξε το email σου για οδηγίες.');return;}
        if(state.mode==='register'&&!document.getElementById('account-privacy').checked){showStatus('Χρειάζεται να διαβάσεις την ενημέρωση λογαριασμού.');return;}
        if(!state.profileRequired)acceptAuth(await authCall(state.mode==='register'?'signUp':'signInWithPassword',{email,password,returnSecureToken:true}));
        const registration=state.mode==='register'?{researchConsent:document.getElementById('account-research').checked}:null;
        try{await finishLogin(registration);}catch(error){if(error.message==='PROFILE_REQUIRED')requireProfile();else throw error;}
      }catch(error){showStatus(safeMessage(error.message));}finally{document.getElementById('account-password').value='';setBusy(false);}});
    document.getElementById('account-back').addEventListener('click',()=>{overlay.hidden=true;document.getElementById('unity-canvas')?.focus();});
    document.getElementById('account-signout').addEventListener('click',signOut);
    document.getElementById('account-profile-research').addEventListener('change',event=>setResearchConsent(event.target.checked));
    const control=document.createElement('button');control.id='account-control';control.type='button';control.textContent='Λογαριασμός';control.hidden=true;control.addEventListener('click',showProfile);document.body.appendChild(control);mode('login');}
  function showProfile(){if(!state.ready)return;profileMode=true;overlay.hidden=false;form.hidden=true;document.querySelector('.account-tabs').hidden=true;document.getElementById('account-profile').hidden=false;document.getElementById('account-profile-research').checked=!!state.profile.researchConsent;document.getElementById('account-title').textContent='Ο λογαριασμός σου';showStatus('');}
  async function setResearchConsent(consent){const c=current();if(!c||!state.profile)return false;const preference={...state.profile,researchConsent:consent===true,consentVersion,consentUtc:new Date().toISOString()};
    // Revocation must stop capture immediately, even while offline. Keep only this
    // account's preference pending until the owner can reconnect.
    state.profile=preference;researchContext();send('OnResearchConsent',consent===true?'true':'false');
    try{c.pendingProfile=preference;store(prefix+'profile-'+c.auth.uid,preference);store(prefix+'consent-pending-'+c.auth.uid,preference);await pump(c);showStatus(c.pendingProfile?'Η προτίμησή σου εφαρμόστηκε τοπικά και θα συγχρονιστεί όταν υπάρχει σύνδεση.':'Η προτίμησή σου αποθηκεύτηκε.');return true;}
    catch{showStatus('Η προτίμησή σου εφαρμόστηκε στην τρέχουσα συνεδρία. Ο browser δεν επέτρεψε τη διατήρησή της.');return false;}}
  function showLogin(){mount();state.profileRequired=false;document.getElementById('account-email').readOnly=false;overlay.hidden=false;form.hidden=false;document.querySelector('.account-tabs').hidden=false;document.getElementById('account-profile').hidden=true;mode('login');}
  function signOut(){const old=state.auth?.uid;if(old){const c=cloudContexts.get(old);if(c)clearTimeout(c.timer);cloudContexts.delete(old);}state.ready=false;state.auth=null;state.profile=null;state.slots=[];try{localStorage.removeItem(authKey);}catch{}researchContext();send('OnAccountSignedOut','');if(typeof document!=='undefined'){document.getElementById('account-control').hidden=true;showLogin();}}
  async function attach(instance){state.instance=instance;mount();showLogin();if(state.auth){setBusy(true);showStatus('Επαναφορά συνεδρίας…');try{await finishLogin(null);}catch(error){if(error.message==='PROFILE_REQUIRED')requireProfile();else showStatus(safeMessage(error.message));}finally{setBusy(false);}}}
  function syncApplied(){if(!state.ready)return;overlay.hidden=true;document.getElementById('account-control').hidden=false;document.getElementById('unity-canvas')?.focus();}
  function syncFailed(){state.ready=false;showStatus('Δεν ολοκληρώθηκε η εισαγωγή των αποθηκεύσεων. Η πρόοδος δεν διαγράφηκε. Δοκίμασε ξανά τη σύνδεση.');}
  window.DWAccount={attach,bridge(name){state.bridge=name;},syncApplied,syncFailed,upload:(slot,json)=>safeQueue(slot,json,'put'),remove:slot=>safeQueue(slot,'','delete'),
    login:(email,password)=>authenticate(email,password),register:(email,password,researchConsent)=>authenticate(email,password,{researchConsent:!!researchConsent}),resetPassword:email=>authCall('sendOobCode',{requestType:'PASSWORD_RESET',email}),
    show:showProfile,signOut,setResearchConsent,ready:()=>state.ready,context:()=>state.auth&&state.profile?{uid:state.auth.uid,researchParticipantId:state.profile.researchParticipantId,researchConsent:state.profile.researchConsent}:null};
  addEventListener('online',()=>pump(current()));
})();
