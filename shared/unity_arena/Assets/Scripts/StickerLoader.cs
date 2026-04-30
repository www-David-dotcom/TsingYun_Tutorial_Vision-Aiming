using UnityEngine;

namespace TsingYun.UnityArena
{
    // Loads a deterministic MNIST sample matching the parent Chassis.Number
    // and applies it as the sticker texture on all four ArmorPlate children.
    // Real RM uses identical printed stickers on all four plates of one
    // robot; we mirror that — one MNIST pick per chassis per episode, four
    // plates share it.
    //
    // Asset layout (Resources):
    //   Assets/Resources/MNIST/{0..9}/000.png ... 049.png  (samples_per_digit
    //   per digit; populate via tools/scripts/extract_mnist_stickers.py).
    //
    // Determinism: SeedRng.NextInt selects the sample index, so the same
    // episode seed picks the same MNIST PNG on every replay. Call
    // LoadStickerForCurrentNumber AFTER SeedRng.Reseed for the episode.
    [RequireComponent(typeof(Chassis))]
    public class StickerLoader : MonoBehaviour
    {
        [SerializeField] private string mnistResourceRoot = "MNIST";
        [SerializeField] private int samplesPerDigit = 50;

        private Chassis _chassis;
        private ArmorPlate[] _plates;

        private void Awake()
        {
            _chassis = GetComponent<Chassis>();
            _plates = GetComponentsInChildren<ArmorPlate>(includeInactive: true);
        }

        public void LoadStickerForCurrentNumber()
        {
            int n = _chassis.Number;
            if (n < 0 || n > 9)
            {
                Debug.LogWarning($"[StickerLoader] {_chassis.Team}: chassis Number={n} out of MNIST range 0-9");
                return;
            }
            int sampleIdx = SeedRng.NextInt(samplesPerDigit);
            string path = $"{mnistResourceRoot}/{n}/{sampleIdx:000}";
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[StickerLoader] missing MNIST texture at Resources/{path}.png; run tools/scripts/extract_mnist_stickers.py and copy output to Assets/Resources/MNIST/");
                return;
            }
            foreach (var plate in _plates)
            {
                plate.ApplySticker(tex);
            }
        }
    }
}
