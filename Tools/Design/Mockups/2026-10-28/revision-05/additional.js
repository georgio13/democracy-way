(()=>{
 const root=document.getElementById('dw-kairos-new');
 function current(){return [...root.querySelectorAll('[data-variant]')].find(el=>!el.hidden)?.dataset.variant;}
 function go(name){const variants=[...root.querySelectorAll('[data-variant]')];const from=variants.findIndex(el=>!el.hidden);const to=variants.findIndex(el=>el.dataset.variant===name);const next=root.querySelector('.viz-carousel-next');if(!next||from<0||to<0)return;for(let i=0;i<(to-from+variants.length)%variants.length;i++)next.click();}
 let previous='Διαβούλευση';
 root.querySelectorAll('.ks-secondary').forEach(button=>{const label=button.textContent.trim();if(label==='Λεξικό'||label==='Ρυθμίσεις')button.addEventListener('click',()=>{previous=current()||'Διαβούλευση';go(label);});});
 root.querySelectorAll('[data-return],[data-settings-done]').forEach(button=>button.addEventListener('click',()=>go(previous)));
 const terms={assembly:['Εκκλησία του Δήμου','Η συνέλευση των Αθηναίων πολιτών. Οι συμμετέχοντες συζητούσαν δημόσιες υποθέσεις, άκουγαν τους ομιλητές και έπαιρναν αποφάσεις με ψηφοφορία.'],council:['Βουλή','Η Βουλή προετοίμαζε τα θέματα που θα συζητούσε η Εκκλησία του Δήμου και παρακολουθούσε τις υποθέσεις της πόλης.'],prytany:['Πρυτανεία','Περίοδος κατά την οποία οι βουλευτές μίας φυλής αναλάμβαναν τα καθήκοντα της πρυτανείας. Στο παιχνίδι σηματοδοτεί ένα κεφάλαιο της πολιτικής σου διαδρομής.']};
 root.querySelectorAll('[data-term]').forEach(button=>button.addEventListener('click',()=>{root.querySelectorAll('[data-term]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));const text=terms[button.dataset.term];root.querySelector('[data-term-heading]').textContent=text[0];root.querySelector('[data-term-copy]').textContent=text[1];}));
 for(const name of ['music','sfx']){const input=root.querySelector('#attic-'+name);const output=root.querySelector('#attic-'+name+'-value');input.addEventListener('input',()=>output.textContent=input.value+'%');}
})();
