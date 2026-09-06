using UnityEngine;
namespace VoidFall.Runtime
{
    public sealed class CrascendoVisualAsset : ScriptableObject
    {
        [SerializeField] private Sprite[] _ground = new Sprite[3];
        [SerializeField] private Sprite[] _wash = new Sprite[3];
        [SerializeField] private TextAsset _tears;
        public bool IsValid => _ground != null && _ground.Length == 3 && _ground[0] != null && _ground[1] != null && _ground[2] != null && _wash != null && _wash.Length == 3 && _wash[0] != null && _wash[1] != null && _wash[2] != null && _tears != null;
        public Sprite Ground(int stage) => _ground[Mathf.Clamp(stage, 0, 2)];
        public Sprite Wash(int stage) => _wash[Mathf.Clamp(stage, 0, 2)];
        public string Tears => _tears != null ? _tears.text : "{}";
    }
}
