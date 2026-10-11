using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Audio;
using RealityEngine.Player;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Base for a Crystal Lab bench. Station frame: origin on the floor at the bench centre, +Z away from the
    /// player (the player stands at z = -1.05), bench top at y = 0.9. Builds bench + guide card + unit-cell model;
    /// subclasses build their apparatus, guide content, control chips and live readout.
    /// </summary>
    public abstract class CrystalStation : MonoBehaviour
    {
        public const float TopY = 0.9f;
        public int Index;
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string ChallengeId { get; }
        protected abstract CrystalUnitCell.Mode CellMode { get; }
        protected abstract string CellTitle { get; }
        protected CrystalGuideCard Card;
        protected CrystalUnitCell Cell;
        protected Transform Bench;

        public Vector3 StandPoint => transform.TransformPoint(new Vector3(0f, 0f, -1.05f));
        public Vector3 LookPoint => transform.TransformPoint(new Vector3(0f, 1.2f, 0.3f));

        public void Build()
        {
            BuildBench();
            BuildStation();
            Card = CrystalGuideCard.Create(transform, new Vector3(0f, 1.72f, 0.52f), Quaternion.Euler(-6f, 0f, 0f), GetContent());
            AddControls(Card);
            Card.SetLive(LiveLine);
            Cell = CrystalUnitCell.Create(transform, new Vector3(1.13f, 1.36f, 0.2f), CellMode, CellTitle);
            Card.PageChanged = p => { if (Cell != null) Cell.Highlight = p == 3 ? 1f : 0f; };
        }

        void BuildBench()
        {
            var b = new GameObject("Bench");
            b.transform.SetParent(transform, false);
            Bench = b.transform;
            Material top = CrystalFx.Solid(new Color(0.09f, 0.11f, 0.13f), 0.2f, 0.7f);
            Material body = CrystalFx.Solid(new Color(0.05f, 0.06f, 0.075f), 0.1f, 0.4f);
            Material strip = CrystalFx.Emissive(new Color(0f, 0.9f, 1f), 1.1f);
            CrystalFx.Prim(PrimitiveType.Cube, "Top", Bench, new Vector3(0f, TopY - 0.025f, 0f), new Vector3(1.8f, 0.05f, 0.7f), top);
            CrystalFx.Prim(PrimitiveType.Cube, "Cabinet", Bench, new Vector3(0f, (TopY - 0.05f) * 0.5f, 0.02f), new Vector3(1.7f, TopY - 0.05f, 0.6f), body);
            CrystalFx.Prim(PrimitiveType.Cube, "Strip", Bench, new Vector3(0f, TopY - 0.035f, -0.352f), new Vector3(1.8f, 0.012f, 0.008f), strip, false);
            var plate = CrystalFx.Text(Bench, "Nameplate", (Index + 1) + "  " + Title.ToUpperInvariant(), 0.55f,
                new Color(0f, 0.9f, 1f, 0.95f), new Vector3(0f, 0.72f, -0.285f), new Vector2(1.6f, 0.14f));
            plate.fontStyle = FontStyles.Bold;
        }

        protected abstract void BuildStation();
        protected abstract CrystalGuideCard.Content GetContent();
        protected abstract void AddControls(CrystalGuideCard card);
        protected abstract string LiveLine();

        /// <summary>Bench-top local point helper.</summary>
        protected static Vector3 OnTop(float x, float y, float z) => new Vector3(x, TopY + y, z);
    }

    /// <summary>
    /// Press-to-toggle part (gas valve, lamp switch...): XR select (ray or direct grip) or desktop E toggles it.
    /// </summary>
    public sealed class CrystalToggle : MonoBehaviour, IQhysicsInteractable
    {
        public string Label = "Toggle";
        public Action Toggled;
        XRSimpleInteractable _ix;

        void Awake()
        {
            _ix = gameObject.AddComponent<XRSimpleInteractable>();
            _ix.selectEntered.AddListener(OnSelect);
        }

        void OnDestroy()
        {
            if (_ix != null)
                _ix.selectEntered.RemoveListener(OnSelect);
        }

        void OnSelect(SelectEnterEventArgs e)
        {
            Fire();
            CrystalFx.PulseBoth(0.12f, 0.02f);
        }

        void Fire()
        {
            QhysicsSfx.PlayAt(SfxId.UiClick, transform.position, 0.5f);
            if (Toggled != null)
                Toggled();
        }

        public string InteractLabel => Label;
        public bool CanInteract(GameObject actor) => true;
        public void Interact(GameObject actor) => Fire();
        public void SetHover(bool hovered) { }
    }

    /// <summary>
    /// Keeps a station-owned loose object (hammer, pestle, seed rod) from wandering off: if it is not held and has
    /// been more than 2.5 m from its rest spot (or fallen below the bench) for 4 s, it is put back.
    /// </summary>
    public sealed class CrystalHomeReturn : MonoBehaviour
    {
        public Vector3 HomePos;
        public Quaternion HomeRot;
        public Transform Frame;
        XRGrabInteractable _grab;
        Rigidbody _rb;
        float _awaySince = -1f;

        public static CrystalHomeReturn Attach(GameObject go, Transform frame)
        {
            var h = go.AddComponent<CrystalHomeReturn>();
            h.Frame = frame;
            h.HomePos = frame != null ? frame.InverseTransformPoint(go.transform.position) : go.transform.position;
            h.HomeRot = frame != null ? Quaternion.Inverse(frame.rotation) * go.transform.rotation : go.transform.rotation;
            return h;
        }

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _rb = GetComponent<Rigidbody>();
        }

        Vector3 Home => Frame != null ? Frame.TransformPoint(HomePos) : HomePos;

        void Update()
        {
            if (_grab != null && _grab.isSelected)
            {
                _awaySince = -1f;
                return;
            }
            Vector3 home = Home;
            bool away = (transform.position - home).sqrMagnitude > 2.5f * 2.5f || transform.position.y < home.y - 1.2f;
            if (!away)
            {
                _awaySince = -1f;
                return;
            }
            if (_awaySince < 0f)
                _awaySince = Time.time;
            if (Time.time - _awaySince > 4f)
                PutBack();
        }

        public void PutBack()
        {
            _awaySince = -1f;
            transform.position = Home;
            transform.rotation = Frame != null ? Frame.rotation * HomeRot : HomeRot;
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
