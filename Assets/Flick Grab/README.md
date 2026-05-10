# Herramienta Flick Grab (VR)

Esta herramienta permite atraer objetos hacia las manos mediante un movimiento en arco (parábola) al apuntar y pulsar el gatillo.

## Estructura de Archivos
- `IFlickGrabbable.cs`: Interfaz base para objetos atraíbles.
- `FlickGrabbable.cs`: Implementación estándar que maneja feedback visual y movimiento.
- `FlickGrabber.cs`: Componente para los mandos (XR Controllers).
- `FlickGrabUtils.cs`: Utilidades matemáticas para la trayectoria.

## Configuración
1. **En los Mandos:**
   - Añade el componente `FlickGrabber` a cada mando (Left Hand / Right Hand).
   - Asigna la acción de entrada (Input Action) correspondiente al gatillo trasero (Trigger) en el campo `Grab Action`.
   - Ajusta el `Max Distance` y el `Grabbable Tag` (por defecto "Grabbable").

2. **En los Objetos:**
   - Opción A: Simplemente ponle el Tag especificado (ej: "Grabbable"). El sistema le añadirá automáticamente el componente `FlickGrabbable` la primera vez que lo apuntes.
   - Opción B: Añade el componente `FlickGrabbable` manualmente para personalizar el color de highlight, la altura del arco o la duración del viaje.

## Requisitos
- Unity XR Interaction Toolkit (o cualquier sistema que use el Input System de Unity).
- Los objetos deben tener un `Collider` para ser detectados por el Raycast.
- Los objetos deben tener un `Renderer` si se desea feedback visual automático.
