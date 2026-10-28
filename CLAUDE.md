# Κανόνες project
- Unity [έκδοση], WebGL, TextMeshPro, Firestore μέσω [plugin].
- Όλο το περιεχόμενο σε ScriptableObjects (Assets/Content). Ποτέ κείμενα ή αριθμοί στον κώδικα.
- Κανένα Instantiate / δημιουργία UI από κώδικα. Τα panels υπάρχουν στη σκηνή και συνδέονται από τον Inspector.
- Οι κανόνες του παιχνιδιού είναι ΜΟΝΟ στο Assets/Scripts/Core/AssemblyRules.cs και EndingRules.cs. Μη τους αλλάζεις χωρίς να το πεις.
- Η ψήφος του παίκτη δεν αλλάζει ποτέ το αποτέλεσμα (ερευνητικός κανόνας).
- Το Assets/Scripts/Core/README.md περιγράφει τη ροή του πυρήνα.
- ΕΝΑ σύστημα, ΜΙΑ δομή: Art, Audio, Content, Editor, Fonts, Plugins, Resources, Scenes, Scripts, TextMesh Pro, WebGLTemplates. Όχι νέοι top-level φάκελοι.
- Μικρά commits, ένα θέμα το καθένα. Πριν από αλλαγές σε scene αρχεία, ρώτα.
