import * as THREE from 'three';

export const SIGN_SCALE = .96 * .8;
export const CHAIN_SCALE = 1.75;

// Build a rigid assembly once. Its hinge passes through both upper anchors,
// so they stay fixed and no part needs to stretch during the animation.
export function createSuspension(sign, chainObjects) {
  sign.position.set(0, .045, 0);
  sign.rotation.set(-.055, -.27, -.095);
  sign.scale.setScalar(SIGN_SCALE);
  sign.updateMatrixWorld(true);

  const anchors = [];
  const chainTips = [];
  for (const [index, object] of chainObjects.entries()) {
    const height = new THREE.Box3().setFromObject(object).getSize(new THREE.Vector3()).y;
    const attachment = new THREE.Vector3(index === 0 ? -.78 : .78, .57, .055)
      .applyMatrix4(sign.matrixWorld);
    const length = height * CHAIN_SCALE;
    const anchor = attachment.clone().add(new THREE.Vector3(0, length, 0));
    object.scale.setScalar(CHAIN_SCALE);
    object.rotation.set(0, -.12, 0);
    object.position.copy(attachment).add(anchor).multiplyScalar(.5);
    anchors.push(anchor);
    chainTips.push({top:new THREE.Vector3(0, height / 2, 0), bottom:new THREE.Vector3(0, -height / 2, 0)});
  }

  const pivot = anchors[0].clone().add(anchors[1]).multiplyScalar(.5);
  const axis = anchors[1].clone().sub(anchors[0]).normalize();
  const assembly = new THREE.Group();
  assembly.name = 'rigid-billboard-suspension';
  assembly.position.copy(pivot);
  for (const object of [sign, ...chainObjects]) {
    object.position.sub(pivot);
    assembly.add(object);
  }
  return {assembly, axis, anchors, chainTips};
}
