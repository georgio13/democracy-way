using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Όλο το αφηγηματικό και ερευνητικό περιεχόμενο που ζούσε στα JSON του Resources,
    /// ως ένα ScriptableObject επεξεργάσιμο από τον Inspector. Συνδέεται στο GameConfiguration
    /// και δένεται στο JsonContent κατά το Awake. Χτίζεται μία φορά από τον μετατροπέα
    /// (Tools → Content → Build Content Library) και από εκεί και πέρα είναι η πηγή αλήθειας.
    /// </summary>
    [CreateAssetMenu(menuName = "Democracy Way/Content Library", fileName = "ContentLibrary")]
    public sealed class ContentLibrary : ScriptableObject
    {
        [Header("Ρυθμίσεις")]
        public GameSettings settings = new GameSettings();

        [Header("Πρόλογος & πρυτανείες")]
        public ProloguePage[] prologuePages;
        public DemocracyQuote[] prytanyQuotes;

        [Header("Βιογραφία")]
        public BiographyStep[] biographySteps;
        public TribeData[] tribes;
        public string[] economicClassNames;
        public NarrativeChoice[] economicClassChoices;
        public NarrativeChoice[] playerNameChoices;
        public NarrativeChoice[] professionChoices;

        [Header("Κείμενα διεπαφής")]
        public InterfaceTextEntry[] interfaceText;

        [Header("Όργανα έρευνας")]
        public DemographicsContent demographics;
        public KnowledgeFile knowledge;
        public MeegaFile meega;
        public TendenciesFile tendencies;
        public TendenciesFile participation;
    }
}
