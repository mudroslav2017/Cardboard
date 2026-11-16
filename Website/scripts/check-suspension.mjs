import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import * as THREE from 'three';
import {createSuspension, SIGN_SCALE, CHAIN_SCALE} from '../dist/suspension.js';

// Inspect the actual exported geometry without loading textures or a browser.
async function readModel(name) {
  const data = await fs.readFile(new URL(`../dist/models/${name}.glb`, import.meta.url));
  const jsonLength = data.readUInt32LE(12);
  const json = JSON.parse(data.subarray(20, 20 + jsonLength).toString());
  const binary = data.subarray(20 + jsonLength + 8);
  const scene = new THREE.Group();
  for (const mesh of json.meshes) for (const primitive of mesh.primitives) {
    const accessor = json.accessors[primitive.attributes.POSITION];
    const view = json.bufferViews[accessor.bufferView];
    const offset = (view.byteOffset || 0) + (accessor.byteOffset || 0);
    const positions = new Float32Array(accessor.count * 3);
    for (let i = 0; i < positions.length; i++) positions[i] = binary.readFloatLE(offset + i * 4);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    scene.add(new THREE.Mesh(geometry));
  }
  return scene;
}
const sign = await readModel('billboard');
const chains = await Promise.all(['chain-1', 'chain-2'].map(readModel));
const {assembly, axis, anchors, chainTips} = createSuspension(sign, chains);
const localAttachments = [-.78, .78].map(x => new THREE.Vector3(x, .57, .055));
assert.equal(SIGN_SCALE / .96, .8, 'The billboard must be 20% smaller');
let maxAnchorDrift = 0;
let maxAttachmentGap = 0;
const depths = [];
for (const angle of [-.24, -.12, 0, .12, .24]) {
  assembly.quaternion.setFromAxisAngle(axis, angle);
  assembly.updateMatrixWorld(true);
  for (const [index, chain] of chains.entries()) {
    assert.deepEqual(chain.scale.toArray(), [CHAIN_SCALE, CHAIN_SCALE, CHAIN_SCALE]);
    const top = chainTips[index].top.clone().applyMatrix4(chain.matrixWorld);
    const bottom = chainTips[index].bottom.clone().applyMatrix4(chain.matrixWorld);
    const attachment = localAttachments[index].clone().applyMatrix4(sign.matrixWorld);
    maxAnchorDrift = Math.max(maxAnchorDrift, top.distanceTo(anchors[index]));
    maxAttachmentGap = Math.max(maxAttachmentGap, bottom.distanceTo(attachment));
    const worldScale = new THREE.Vector3().setFromMatrixScale(chain.matrixWorld);
    assert.ok(Math.abs(worldScale.x - worldScale.y) < 1e-10 && Math.abs(worldScale.y - worldScale.z) < 1e-10, 'Chain proportions must stay unchanged');
  }
  depths.push(new THREE.Vector3().setFromMatrixPosition(sign.matrixWorld).z);
}
assert.ok(maxAnchorDrift < 1e-10, 'Upper anchors must remain fixed');
assert.ok(maxAttachmentGap < 1e-10, 'Chain ends must stay attached');
assert.ok(Math.max(...depths) - Math.min(...depths) > .7, 'The sign must move in depth');
console.log(JSON.stringify({signScale:SIGN_SCALE,chainScale:CHAIN_SCALE,maxAnchorDrift,maxAttachmentGap,depthTravel:Math.max(...depths)-Math.min(...depths)},null,2));
