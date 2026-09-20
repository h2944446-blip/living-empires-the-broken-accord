using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LivingEmpires
{
    // Resolve gesture ownership before the controller commits world clicks.
    [DefaultExecutionOrder(-100)]
    public sealed class StrategyCamera : MonoBehaviour
    {
        public bool ControlsEnabled = true;
        public bool ArmyControls;
        public static readonly Vector3 HomeFocus = new Vector3(-17, 0, 2);
        public const float HomeYaw = -18, HomePitch = 49, HomeDistance = 38;
        public Camera View;
        public Vector3 Focus = new Vector3(-10, 0, 0);
        public float Yaw, Pitch = 52, Distance = 38;
        public bool EdgePanEnabled = true;
        public bool LeftClickReleased { get; private set; }
        public bool RightClickPressed { get; private set; }
        public bool RightClickReleased { get; private set; }
        public bool SelectionBoxReleased { get; private set; }
        public bool SelectionBoxActive => gesture == Gesture.ArmySelect && moved;
        public Vector2 SelectionStart { get; private set; }
        public Vector2 SelectionEnd { get; private set; }
        public bool IsDragging { get; private set; }
        public string ActiveGesture => IsDragging ? gesture.ToString() : "None";

        enum Gesture { None, GroundPan, RightPan, Orbit, Click, ArmySelect }
        Gesture gesture;
        Vector2 pressPosition, previousPointer;
        bool moved;
        float edgeDwell;
        const float DragThreshold = 6;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        PointerEventData pointerData;
        EventSystem pointerSystem;

        void Awake() { View = GetComponent<Camera>(); Home(); Apply(true); }
        public void Home()
        {
            Focus = HomeFocus;
            Yaw = HomeYaw; Pitch = HomePitch; Distance = HomeDistance;
            ResetGesture();
        }
        public void FocusOn(Vector3 point) { Focus = point; ClampTargets(); }
        public void ZoomBy(float delta) { Distance = Mathf.Clamp(Distance + delta, 15, 68); }
        public void RotateBy(float degrees) { Yaw = Mathf.Repeat(Yaw + degrees, 360); }
        public void SetEdgePanning(bool enabled) { EdgePanEnabled = enabled; edgeDwell = 0; }
        public void CancelGesture() { ResetGesture(); LeftClickReleased = RightClickReleased = SelectionBoxReleased = false; }

        bool InputAllowed()
        {
            var game = GameController.Instance;
            return ControlsEnabled && Application.isFocused &&
                (game == null || (!game.InMenu && !game.Modal && !game.BenchmarkRunning));
        }
        public bool PointerOverUI()
        {
            var system = EventSystem.current;
            var mouse = Mouse.current;
            if (system == null || mouse == null) return false;
            if (pointerData == null || pointerSystem != system)
            {
                pointerSystem = system;
                pointerData = new PointerEventData(system);
            }
            pointerData.Reset();
            pointerData.position = mouse.position.ReadValue();
            uiHits.Clear();
            system.RaycastAll(pointerData, uiHits);
            foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }
        void Update()
        {
            LeftClickReleased = false;
            RightClickPressed = false;
            RightClickReleased = false;
            SelectionBoxReleased = false;
            if (!InputAllowed()) { ResetGesture(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            var keys = Keyboard.current;
            if (keys != null)
            {
                var move = new Vector3(
                    (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0), 0,
                    (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0));
                if (move.sqrMagnitude > 1) move.Normalize();
                Pan(move, dt);
                RotateBy(((keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0)) * dt * 70);
                if (keys.homeKey.wasPressedThisFrame) Home();
            }
            // A mouse does not depend on a keyboard device being present.
            var mouse = Mouse.current;
            if (mouse != null) ReadMouse(mouse, dt);
            ClampTargets();
        }
        void ReadMouse(Mouse mouse, float dt)
        {
            Vector2 point = mouse.position.ReadValue();
            bool inside = point.x >= 0 && point.y >= 0 && point.x < Screen.width && point.y < Screen.height;
            if (!inside) { ResetGesture(); return; }
            bool overUI = PointerOverUI();
            bool placing = GameController.Instance != null && GameController.Instance.BuildType != "";
            if (!overUI && gesture == Gesture.None)
            {
                if (mouse.middleButton.wasPressedThisFrame) Begin(Gesture.Orbit, point);
                else if (mouse.rightButton.wasPressedThisFrame)
                {
                    RightClickPressed = true;
                    if (!placing) Begin(Gesture.RightPan, point);
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                    Begin(ArmyControls && !placing ? Gesture.ArmySelect : !placing && !BuildingAt(point) ? Gesture.GroundPan : Gesture.Click, point);
            }
            if (gesture != Gesture.None)
            {
                if (gesture == Gesture.ArmySelect) SelectionEnd = point;
                if ((point - pressPosition).sqrMagnitude >= DragThreshold * DragThreshold) moved = true;
                bool held = gesture == Gesture.Orbit ? mouse.middleButton.isPressed :
                    gesture == Gesture.RightPan ? mouse.rightButton.isPressed : mouse.leftButton.isPressed;
                if (held && moved)
                {
                    IsDragging = gesture != Gesture.Click;
                    if (gesture == Gesture.Orbit)
                    {
                        Vector2 delta = point - previousPointer;
                        RotateBy(delta.x * .22f);
                        Pitch = Mathf.Clamp(Pitch - delta.y * .16f, 32, 72);
                    }
                    else if (gesture == Gesture.GroundPan || gesture == Gesture.RightPan)
                    {
                        Vector3 before, after;
                        if (GroundAt(previousPointer, out before) && GroundAt(point, out after)) Focus += before - after;
                    }
                }
                if (!held)
                {
                    LeftClickReleased = (gesture == Gesture.GroundPan || gesture == Gesture.Click || gesture == Gesture.ArmySelect) &&
                        !moved && !overUI && mouse.leftButton.wasReleasedThisFrame;
                    RightClickReleased = gesture == Gesture.RightPan && !moved && !overUI && mouse.rightButton.wasReleasedThisFrame;
                    SelectionBoxReleased = gesture == Gesture.ArmySelect && moved && !overUI && mouse.leftButton.wasReleasedThisFrame;
                    ResetGesture();
                }
                previousPointer = point;
                edgeDwell = 0;
                return;
            }
            // A press originating in the HUD never becomes a world gesture.
            if (overUI || mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            { edgeDwell = 0; return; }
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > .01f) { ZoomBy(-wheel * .018f); edgeDwell = 0; }
            if (EdgePanEnabled && Mathf.Abs(wheel) < .01f)
            {
                float band = Mathf.Clamp(Screen.height * .018f, 12, 28);
                Vector3 edge = new Vector3(EdgeAxis(point.x, Screen.width, band), 0, EdgeAxis(point.y, Screen.height, band));
                if (edge.sqrMagnitude > .001f)
                {
                    edgeDwell += dt;
                    if (edgeDwell > .18f) Pan(Vector3.ClampMagnitude(edge, 1), dt);
                }
                else edgeDwell = 0;
            }
        }
        static float EdgeAxis(float value, float size, float band)
        {
            if (value < band) return -(1 - value / band);
            if (value > size - band) return 1 - (size - value) / band;
            return 0;
        }
        void Begin(Gesture kind, Vector2 point)
        {
            gesture = kind; pressPosition = previousPointer = point;
            if (kind == Gesture.ArmySelect) SelectionStart = SelectionEnd = point;
            moved = false; IsDragging = false; edgeDwell = 0;
        }
        void ResetGesture() { gesture = Gesture.None; moved = false; IsDragging = false; edgeDwell = 0; }
        void OnApplicationFocus(bool focused) { if (!focused) ResetGesture(); }
        void OnDisable() { ResetGesture(); }
        void Pan(Vector3 direction, float dt) { Focus += Quaternion.Euler(0, Yaw, 0) * direction * Distance * .55f * dt; }
        void ClampTargets()
        {
            Focus = new Vector3(Mathf.Clamp(Focus.x, -39, 35), 0, Mathf.Clamp(Focus.z, -25, 26));
            Pitch = Mathf.Clamp(Pitch, 32, 72); Distance = Mathf.Clamp(Distance, 15, 68);
        }
        bool BuildingAt(Vector2 point)
        {
            RaycastHit hit;
            return View != null && Physics.Raycast(View.ScreenPointToRay(point), out hit, 240) &&
                hit.collider.GetComponentInParent<BuildingView>() != null;
        }
        void LateUpdate() { ClampTargets(); Apply(false); }
        void Apply(bool instant)
        {
            var rotation = Quaternion.Euler(Pitch, Yaw, 0);
            Vector3 target = Focus + rotation * new Vector3(0, 0, -Distance);
            float blend = instant ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18);
            transform.position = Vector3.Lerp(transform.position, target, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
        }
        bool GroundAt(Vector2 screen, out Vector3 point)
        {
            point = Vector3.zero;
            if (View == null) return false;
            Ray ray = View.ScreenPointToRay(screen);
            float distance;
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance)) return false;
            point = ray.GetPoint(distance); return true;
        }
        public bool Ground(out Vector3 point)
        {
            point = Vector3.zero;
            return Mouse.current != null && GroundAt(Mouse.current.position.ReadValue(), out point);
        }
    }
}
