import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import * as THREE from 'three';
import {FBXLoader} from 'three/addons/loaders/FBXLoader.js';
import {GLTFExporter} from 'three/addons/exporters/GLTFExporter.js';

const sharp = (await import(process.argv[2] || 'sharp')).default;
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.join(root, 'dist/models');
await fs.mkdir(output, {recursive:true});

// FBX images are loaded separately below, so conversion needs no browser or canvas.
const manager = new THREE.LoadingManager();
manager.addHandler(/.*/, {
  setPath(value) { this.path = value; return this; },
  load() { return new THREE.Texture(); }
});
globalThis.FileReader = class {
  readAsArrayBuffer(blob) {
    blob.arrayBuffer().then(result => { this.result = result; this.onloadend?.(); });
  }
  readAsDataURL(blob) {
    blob.arrayBuffer().then(result => {
      this.result = `data:${blob.type};base64,${Buffer.from(result).toString('base64')}`;
      this.onloadend?.();
    });
  }
};
const pad = buffer => Buffer.concat([buffer, Buffer.alloc((4 - buffer.length % 4) % 4)]);

for (const [name, source, maxSize] of [
  ['billboard', 'Billboard/Billboard', 2048],
  ['chain-1', 'Billboard/Chains/Chain_1', 1024],
  ['chain-2', 'Billboard/Chains/Chain_2', 1024]
]) {
  const bytes = await fs.readFile(path.join(root, 'Models_Site', `${source}.fbx`));
  const fbx = new FBXLoader(manager).parse(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
  fbx.updateMatrixWorld(true);
  const scene = new THREE.Scene();
  fbx.traverse(object => {
    if (!object.isMesh) return;
    const geometry = object.geometry.clone().applyMatrix4(object.matrixWorld);
    geometry.rotateY(-Math.PI / 2);
    geometry.scale(.01, .01, .01);
    geometry.center();
    // FBX texture coordinates use a bottom origin; glTF images use a top origin.
    const uv = geometry.attributes.uv;
    for (let i = 0; i < uv.count; i++) uv.setY(i, 1 - uv.getY(i));
    const material = new THREE.MeshStandardMaterial({color:0xffffff, roughness:1, metalness:0, side:THREE.DoubleSide});
    material.name = name;
    const mesh = new THREE.Mesh(geometry, material);
    mesh.name = name;
    scene.add(mesh);
  });
  const gltf = await new GLTFExporter().parseAsync(scene, {binary:false});
  const geometryBytes = pad(Buffer.from(gltf.buffers[0].uri.split(',')[1], 'base64'));
  const texture = await sharp(path.join(root, 'Models_Site', `${source}.png`))
    .resize({width:maxSize, height:maxSize, fit:'inside', withoutEnlargement:true})
    .webp({quality:92, alphaQuality:100}).toBuffer();
  const imageBytes = pad(texture);
  gltf.images = [{bufferView:gltf.bufferViews.length, mimeType:'image/webp'}];
  gltf.bufferViews.push({buffer:0, byteOffset:geometryBytes.length, byteLength:texture.length});
  gltf.samplers = [{magFilter:9729, minFilter:9987, wrapS:10497, wrapT:10497}];
  gltf.textures = [{sampler:0, extensions:{EXT_texture_webp:{source:0}}}];
  gltf.extensionsUsed = [...(gltf.extensionsUsed || []), 'EXT_texture_webp'];
  gltf.extensionsRequired = ['EXT_texture_webp'];
  for (const material of gltf.materials) material.pbrMetallicRoughness.baseColorTexture = {index:0};
  const binary = Buffer.concat([geometryBytes, imageBytes]);
  gltf.buffers = [{byteLength:binary.length}];
  const json = Buffer.from(JSON.stringify(gltf));
  const jsonPadding = Buffer.alloc((4 - json.length % 4) % 4, 0x20);
  const jsonChunk = Buffer.concat([json, jsonPadding]);
  const header = Buffer.alloc(12);
  header.writeUInt32LE(0x46546c67, 0);
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(12 + 8 + jsonChunk.length + 8 + binary.length, 8);
  const jsonHeader = Buffer.alloc(8);
  jsonHeader.writeUInt32LE(jsonChunk.length, 0);
  jsonHeader.writeUInt32LE(0x4e4f534a, 4);
  const binHeader = Buffer.alloc(8);
  binHeader.writeUInt32LE(binary.length, 0);
  binHeader.writeUInt32LE(0x004e4942, 4);
  const glb = Buffer.concat([header, jsonHeader, jsonChunk, binHeader, binary]);
  await fs.writeFile(path.join(output, `${name}.glb`), glb);
  console.log(`${name}.glb: ${(glb.length / 1024).toFixed(1)} KB`);
}

// Serve all runtime code from the same host, without CDN dependencies.
const threeRoot = path.join(root, 'node_modules/three');
for (const file of ['build/three.module.min.js', 'build/three.core.min.js', 'examples/jsm/loaders/GLTFLoader.js', 'examples/jsm/utils/BufferGeometryUtils.js', 'LICENSE']) {
  const destination = path.join(root, 'dist/vendor/three', file);
  await fs.mkdir(path.dirname(destination), {recursive:true});
  await fs.copyFile(path.join(threeRoot, file), destination);
}
