using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AgroLife.Game
{
    /// <summary>Cámara ortográfica del mapa: zoom con la rueda hacia el cursor, arrastre con botón derecho o del medio, WASD/flechas; limitada al contorno.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CamaraMapa : MonoBehaviour
    {
        public Rect Limites;
        public float TamanioMin = 5f;
        public Func<Vector2, bool> PunteroSobreUI;

        Camera cam;
        Vector3? agarre; // punto del mundo que quedó bajo el cursor al empezar a arrastrar

        public static Vector3 Limitar(Vector3 pos, float tamanio, float aspecto, Rect limites)
        {
            float medioAlto = tamanio, medioAncho = tamanio * aspecto;
            float x = 2 * medioAncho >= limites.width ? limites.center.x : Mathf.Clamp(pos.x, limites.xMin + medioAncho, limites.xMax - medioAncho);
            float y = 2 * medioAlto >= limites.height ? limites.center.y : Mathf.Clamp(pos.y, limites.yMin + medioAlto, limites.yMax - medioAlto);
            return new Vector3(x, y, pos.z);
        }

        public static float ZoomHacia(float tamanio, float rueda, float min, float max) =>
            Mathf.Clamp(tamanio * Mathf.Pow(1.15f, -rueda), min, max);

        void Awake() => cam = GetComponent<Camera>();

        float TamanioMax => Mathf.Max(Limites.height / 2, Limites.width / 2 / cam.aspect);

        void LateUpdate()
        {
            if (Limites.width <= 0) return;
            var mouse = Mouse.current;
            var teclado = Keyboard.current;
            if (mouse != null)
            {
                Vector2 pantalla = mouse.position.ReadValue();
                bool sobreUI = PunteroSobreUI != null && PunteroSobreUI(pantalla);
                float rueda = mouse.scroll.ReadValue().y;
                if (!sobreUI && rueda != 0)
                {
                    var antes = cam.ScreenToWorldPoint(pantalla);
                    cam.orthographicSize = ZoomHacia(cam.orthographicSize, Mathf.Sign(rueda), TamanioMin, TamanioMax);
                    transform.position += antes - cam.ScreenToWorldPoint(pantalla); // el punto bajo el cursor queda fijo
                }
                if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                {
                    if (agarre == null && !sobreUI) agarre = cam.ScreenToWorldPoint(pantalla);
                    else if (agarre != null) transform.position += agarre.Value - cam.ScreenToWorldPoint(pantalla);
                }
                else agarre = null;
            }
            if (teclado != null)
            {
                var dir = new Vector2(
                    (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed ? 1 : 0) - (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed ? 1 : 0),
                    (teclado.wKey.isPressed || teclado.upArrowKey.isPressed ? 1 : 0) - (teclado.sKey.isPressed || teclado.downArrowKey.isPressed ? 1 : 0));
                transform.position += (Vector3)(dir.normalized * cam.orthographicSize * 1.5f * Time.unscaledDeltaTime);
            }
            transform.position = Limitar(transform.position, cam.orthographicSize, cam.aspect, Limites);
        }
    }
}
