using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DetectionEntity : AbstractEntity
{
    public enum DetectionShape { Sphere, Box }

    [Flags]
    public enum EntityTypes
    {
        None = 0,
        Active = 1,
        Passive = 2,
        SoftBody = 4,
        Detection = 8,
        Everything = Active | Passive | SoftBody | Detection
    }
    
    [Serializable]
    public class EntityEvent : UnityEvent<MonoBehaviour> { }

    [Header("Detection Shape")]
    [SerializeField] private DetectionShape shape = DetectionShape.Sphere;
    [SerializeField, Min(0.001f)] private float width = 1f;
    [SerializeField, Min(0.001f)] private float height = 1f;
    [SerializeField, Min(0.001f)] private float length = 1f;

    [Header("Detection Filters")]
    [SerializeField] private EntityTypes detectedTypes = EntityTypes.Active;
    [SerializeField] private LayerMask detectedLayers = ~0;

    [SerializeField] private string[] allowedTags = new string[0];

    [SerializeField] private string[] allowedNames = new string[0];

    [SerializeField] private bool ignoreCloneSuffix = true;

    [Header("Detection Events")]
    [SerializeField] private EntityEvent onEnter = new EntityEvent();
    [SerializeField] private EntityEvent onStay = new EntityEvent();
    [SerializeField] private EntityEvent onExit = new EntityEvent();

    [Header("Detection State (Runtime)")]

    [SerializeField] private List<MonoBehaviour> insideEntities = new List<MonoBehaviour>(32);

    [SerializeField] private List<MonoBehaviour> enteredEntities = new List<MonoBehaviour>(16);

    [SerializeField] private List<MonoBehaviour> exitedEntities = new List<MonoBehaviour>(16);

    private IReadOnlyList<MonoBehaviour> _insideView;
    private IReadOnlyList<MonoBehaviour> _enteredView;
    private IReadOnlyList<MonoBehaviour> _exitedView;

    public IReadOnlyList<MonoBehaviour> InsideEntities => _insideView ??= insideEntities.AsReadOnly();
    public IReadOnlyList<MonoBehaviour> EnteredEntities => _enteredView ??= enteredEntities.AsReadOnly();
    public IReadOnlyList<MonoBehaviour> ExitedEntities => _exitedView ??= exitedEntities.AsReadOnly();

    public EntityEvent OnEnter => onEnter;
    public EntityEvent OnStay => onStay;
    public EntityEvent OnExit => onExit;
    public EntityTypes DetectedTypes { get => detectedTypes; set => detectedTypes = value; }
    public LayerMask DetectedLayers { get => detectedLayers; set => detectedLayers = value; }
    public int DetectedCount => _inside.Count;
    public bool Contains(MonoBehaviour entity) => entity != null && _inside.Contains(entity);
    public DetectionShape Shape => shape;
    public Vector3 BoxSize => new Vector3(Mathf.Max(0.001f, width), Mathf.Max(0.001f, height), Mathf.Max(0.001f, length));

    private HashSet<MonoBehaviour> _inside = new HashSet<MonoBehaviour>();
    private HashSet<MonoBehaviour> _next = new HashSet<MonoBehaviour>();
    private readonly HashSet<MonoBehaviour> _notified = new HashSet<MonoBehaviour>();
    private readonly List<PendingEvent> _events = new List<PendingEvent>(32);
    private DetectionGeometry.Shape _geometry;
    private int _generation;
    private int _evaluatedGeneration;

    private enum EventKind { Enter, Stay, Exit }
    private struct PendingEvent
    {
        public MonoBehaviour Entity;
        public EventKind Kind;
    }

    protected override void Awake()
    {
        base.Awake();
        ClearDetectionLists();
    }

    private void ClearDetectionLists()
    {
        insideEntities.Clear();
        enteredEntities.Clear();
        exitedEntities.Clear();
    }

    protected virtual void OnEnable()
    {
        RefreshDetectionShape();
        if (_physicsManager != null) _physicsManager.Register(this);
    }

    protected virtual void Start()
    {
        _physicsManager = PhysicsManager.Instance;
        if (_physicsManager == null)
        {
            Debug.LogError(name + " : aucun PhysicsManager trouve pour la detection.", this);
            enabled = false;
            return;
        }
        _physicsManager.Register(this);
    }

    protected virtual void OnDisable()
    {
        if (_physicsManager != null) _physicsManager.Unregister(this);
        _generation++;
        _inside.Clear();
        _next.Clear();
        _notified.Clear();
        _events.Clear();
        ClearDetectionLists();
    }

    internal void RefreshDetectionShape()
    {
        RefreshPosition();
        _geometry = shape == DetectionShape.Box
            ? DetectionGeometry.Shape.Box(Position, transform.rotation, BoxSize)
            : DetectionGeometry.Shape.Sphere(Position, Radius);
    }

    internal void Evaluate(List<MonoBehaviour> candidates)
    {
        _evaluatedGeneration = _generation;
        _events.Clear();
        _next.Clear();
        ClearDetectionLists();
        for (int i = 0; i < candidates.Count; i++)
        {
            MonoBehaviour other = candidates[i];
            if (!Accepts(other) || !Overlaps(other) || !_next.Add(other)) continue;
            insideEntities.Add(other);
            bool wasNotified = _notified.Contains(other);
            _events.Add(new PendingEvent { Entity = other, Kind = wasNotified ? EventKind.Stay : EventKind.Enter });
        }
        foreach (MonoBehaviour other in _notified)
        {
            if (_next.Contains(other)) continue;
            _events.Add(new PendingEvent { Entity = other, Kind = EventKind.Exit });
        }

        (_inside, _next) = (_next, _inside);
        _next.Clear();
    }

    internal void DispatchEvents()
    {
        for (int i = 0; i < _events.Count; i++)
        {
            if (this == null || !isActiveAndEnabled || _generation != _evaluatedGeneration) break;
            PendingEvent entry = _events[i];
            if (entry.Kind != EventKind.Exit && (entry.Entity == null || !entry.Entity.isActiveAndEnabled)) continue;
            try
            {
                switch (entry.Kind)
                {
                    case EventKind.Enter:
                        _notified.Add(entry.Entity);
                        enteredEntities.Add(entry.Entity);
                        onEnter.Invoke(entry.Entity);
                        break;
                    case EventKind.Stay: onStay.Invoke(entry.Entity); break;
                    case EventKind.Exit:
                        _notified.Remove(entry.Entity);
                        exitedEntities.Add(entry.Entity);
                        onExit.Invoke(entry.Entity);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
        _events.Clear();
    }

    private bool Accepts(MonoBehaviour other)
    {
        if (other == null || other == this || !other.isActiveAndEnabled) return false;
        EntityTypes type;
        if (other is DetectionEntity) type = EntityTypes.Detection;
        else if (other is SoftBodyEntity) type = EntityTypes.SoftBody;
        else if (other is ActiveEntity) type = EntityTypes.Active;
        else if (other is PassiveEntity) type = EntityTypes.Passive;
        else return false;
        if ((detectedTypes & type) == 0 || (detectedLayers.value & (1 << other.gameObject.layer)) == 0) return false;
        if (allowedTags != null && allowedTags.Length > 0 && !Matches(allowedTags, other.gameObject.tag, false)) return false;
        return allowedNames == null || allowedNames.Length == 0 || Matches(allowedNames, other.gameObject.name, ignoreCloneSuffix);
    }

    private static bool Matches(string[] allowed, string value, bool stripClone)
    {
        const string suffix = "(Clone)";
        int count = value.Length;
        if (stripClone && value.EndsWith(suffix, StringComparison.Ordinal)) count -= suffix.Length;
        for (int i = 0; i < allowed.Length; i++)
        {
            string entry = allowed[i];
            if (!string.IsNullOrEmpty(entry) && entry.Length == count &&
                string.CompareOrdinal(value, 0, entry, 0, count) == 0) return true;
        }
        return false;
    }

    private bool Overlaps(MonoBehaviour other)
    {
        if (other is SoftBodyEntity softBody) return softBody.OverlapsDetection(_geometry);
        if (other is DetectionEntity detection) return DetectionGeometry.Overlaps(_geometry, detection._geometry);
        if (other is PassiveEntity passive)
        {
            DetectionGeometry.Shape target = passive.Shape == PassiveEntity.ColliderShape.Box
                ? DetectionGeometry.Shape.Box(passive.transform.position, passive.transform.rotation, passive.BoxSize)
                : DetectionGeometry.Shape.Sphere(passive.transform.position, passive.Radius);
            return DetectionGeometry.Overlaps(_geometry, target);
        }
        if (other is ActiveEntity active)
            return DetectionGeometry.Overlaps(_geometry, DetectionGeometry.Shape.Sphere(active.Position, active.Radius));
        return false;
    }

    protected virtual void Reset() => colliderGizmoColor = Color.green;

    protected override void OnDrawGizmos()
    {
        if (!DebugEnabled) return;
        Color oldColor = Gizmos.color;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.color = colliderGizmoColor;
        if (shape == DetectionShape.Sphere)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, BoxSize);
        }
        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
}
