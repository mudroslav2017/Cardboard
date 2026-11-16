import * as THREE from 'three';
import {GLTFLoader} from './vendor/three/examples/jsm/loaders/GLTFLoader.js';
import {createSuspension} from './suspension.js?v=rigid-1';

const host = document.getElementById('billboard-scene');
const landing = document.getElementById('landing');
const hitTarget = host.querySelector('.billboard-hit');
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

async function mountBillboard() {
  const renderer = new THREE.WebGLRenderer({alpha:true, antialias:true, powerPreference:'low-power'});
  renderer.setClearColor(0x000000, 0);
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = .85;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.domElement.setAttribute('aria-hidden', 'true');
  host.append(renderer.domElement);

  const scene = new THREE.Scene();
  // Match the former framing at rest, with perspective that reveals depth as
  // the sign swings toward and away from the viewer.
  const camera = new THREE.PerspectiveCamera(28, 900 / 720, .1, 20);
  camera.position.set(0, .32, 2.16 / (2 * Math.tan(THREE.MathUtils.degToRad(14))));
  camera.lookAt(0, .32, 0);
  camera.updateMatrixWorld(true);
  scene.add(new THREE.HemisphereLight(0xe5d0b2, 0x24170e, .9));
  const key = new THREE.DirectionalLight(0xffead0, 1.25);
  key.position.set(2.5, 4, 5);
  scene.add(key);
  // A warm overhead pool of light follows the factory lamp at the upper right.
  const lamp = new THREE.SpotLight(0xffd4a0, 7.5, 9, .78, .85, 2);
  lamp.position.set(1.4, 2, 2.2);
  lamp.target.position.set(-.2, .05, 0);
  lamp.castShadow = true;
  lamp.shadow.mapSize.set(1024, 1024);
  lamp.shadow.bias = -.0004;
  lamp.shadow.normalBias = .004;
  scene.add(lamp, lamp.target);
  const fill = new THREE.DirectionalLight(0x9eaabd, .23);
  fill.position.set(-4, .5, 3);
  scene.add(fill);

  const loader = new GLTFLoader();
  const [signAsset, ...chainAssets] = await Promise.all(
    ['billboard', 'chain-1', 'chain-2'].map(name => loader.loadAsync(`./models/${name}.glb?v=1`))
  );
  const sign = signAsset.scene;
  const anisotropy = Math.min(renderer.capabilities.getMaxAnisotropy(), 8);
  for (const asset of [signAsset, ...chainAssets]) {
    asset.scene.traverse(object => {
      if (!object.isMesh) return;
      object.material.map.anisotropy = anisotropy;
      object.castShadow = true;
      object.receiveShadow = true;
    });
  }
  const {assembly, axis} = createSuspension(sign, chainAssets.map(asset => asset.scene));
  scene.add(assembly);
  // Project the front outline onto a real DOM button. This also works with CSS
  // scaling and touch, and leaves the transparent part of the canvas pass-through.
  const outline = [[-.99,.56,.057],[.95,.56,.057],[1.02,.48,.057],[1.02,-.52,.057],[-.8,-.56,.057],[-1.06,-.35,.057],[-1.06,.43,.057]]
    .map(point => new THREE.Vector3(...point));
  const projected = new THREE.Vector3();
  let hovered = false;
  let focused = false;
  let visible = true;
  let contextLost = false;
  let frame = 0;
  let previousTime = 0;
  let angle = 0;
  let velocity = 0;

  function pose() {
    assembly.quaternion.setFromAxisAngle(axis, angle);
    assembly.updateMatrixWorld(true);
    hitTarget.style.clipPath = `polygon(${outline.map(point => {
      projected.copy(point).applyMatrix4(sign.matrixWorld).project(camera);
      return `${(projected.x + 1) * 50}% ${(1 - projected.y) * 50}%`;
    }).join(',')})`;
  }
  function render() {
    pose();
    renderer.render(scene, camera);
  }
  function isActive() { return visible && !document.hidden && !contextLost; }
  function wake() {
    if (!frame && isActive() && !reducedMotion.matches) {
      previousTime = 0;
      frame = requestAnimationFrame(animate);
    }
  }
  function animate(time) {
    frame = 0;
    if (!isActive()) return;
    const dt = previousTime ? Math.min((time - previousTime) / 1000, .04) : 1 / 60;
    previousTime = time;
    const interacting = hovered || focused;
    const target = interacting ? Math.sin(time / 370) * .14 : 0;
    velocity += ((target - angle) * 7.5 - velocity * (interacting ? 1.7 : 2.2)) * dt;
    angle = THREE.MathUtils.clamp(angle + velocity * dt, -.24, .24);
    render();
    if (interacting || Math.abs(angle) > .0001 || Math.abs(velocity) > .0001) {
      frame = requestAnimationFrame(animate);
    } else {
      angle = velocity = 0;
      render();
    }
  }
  function setHovered(value) {
    if (value === hovered) return;
    hovered = value;
    if (value && !reducedMotion.matches) velocity += .48;
    wake();
  }
  hitTarget.addEventListener('pointerenter', () => setHovered(true));
  hitTarget.addEventListener('pointerleave', () => setHovered(false));
  hitTarget.addEventListener('focus', () => { focused = true; velocity += .25; wake(); });
  hitTarget.addEventListener('blur', () => { focused = false; wake(); });
  hitTarget.addEventListener('click', () => { velocity += .35; wake(); });
  const resize = () => {
    const rect = host.getBoundingClientRect();
    renderer.setSize(Math.max(1, Math.round(rect.width)), Math.max(1, Math.round(rect.height)), false);
    render();
  };
  new ResizeObserver(resize).observe(landing.parentElement);
  new IntersectionObserver(([entry]) => {
    visible = entry.isIntersecting;
    if (visible) { render(); wake(); }
    else { hovered = false; }
  }).observe(host);
  document.addEventListener('visibilitychange', () => {
    if (!document.hidden) { render(); wake(); }
  });
  reducedMotion.addEventListener('change', () => {
    cancelAnimationFrame(frame);
    frame = 0;
    angle = velocity = 0;
    render();
    if (!reducedMotion.matches) wake();
  });
  renderer.domElement.addEventListener('webglcontextlost', event => {
    event.preventDefault();
    contextLost = true;
    host.classList.remove('is-ready');
    landing.classList.remove('billboard-loaded');
  });
  renderer.domElement.addEventListener('webglcontextrestored', () => {
    contextLost = false;
    render();
    host.classList.add('is-ready');
    landing.classList.add('billboard-loaded');
    wake();
  });
  resize();
  hitTarget.disabled = false;
  host.classList.add('is-ready');
  landing.classList.add('billboard-loaded');
}

mountBillboard().catch(error => {
  // Keep the original artwork when WebGL or model loading is unavailable.
  hitTarget.disabled = true;
  host.setAttribute('aria-hidden', 'true');
  host.querySelector('canvas')?.remove();
  console.warn('3D billboard unavailable; using the original artwork.', error);
});
