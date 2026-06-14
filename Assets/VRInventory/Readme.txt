Componente VRInventory

Herramienta con la que el usuario puede crear en el editor anclas a las que una vez el mismo usuario acerca un objeto con la tag especificada por el usuario, se queda agarrado y el usuario lo puede controlar libremente.

- Para crear una ancla, hay que crear un GameObject vacío en la escena y añadir a este el script de InventorySlot, donde el usuario puede controlar la tag que quiera que acepte, el radio en el que acepta el objeto y como sse ve el ancla en el mundo.
- Para que un objeto sea compatible con el ancla, hace falta el componente XRGrabInteractable, InventoryItem, Collider, Rigidbody y la tag específica que tiene que corresponder con el ancla que queramos.