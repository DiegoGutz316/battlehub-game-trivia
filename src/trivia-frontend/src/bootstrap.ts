// Arranque del juego EN MODO INDEPENDIENTE (npm start en el puerto 4002).
// Sirve para desarrollar y probar el juego sin el Shell. El Shell NO usa este archivo:
// el Shell carga directamente ./GameModule a través de remoteEntry.js.
import Aurelia from 'aurelia';
import { DevShell } from './dev/dev-shell';

Aurelia.app(DevShell).start();
