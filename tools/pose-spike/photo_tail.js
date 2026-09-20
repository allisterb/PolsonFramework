// SPIKE step 4: the real input. A photograph, through the full chain.
//
// There is no ground truth here - nobody can say what Bolt's elbow angle "really" was in degrees -
// so this is judged by eye rather than scored. Three panels, because the two failure modes are
// different things and only the first two separate them:
//
//   1. what the DETECTOR saw          - its own skeleton over the photograph
//   2. what the MAPPING made of it    - the mannequin over the photograph
//   3. the mannequin alone            - what a director would actually be handed
//
// `DETECTED` and `PHOTO` are spliced in by the driver.

const ref = reference();
const a = jointsFromMediaPipe(DETECTED, 0.5);
const pose = poseFromJoints(a.joints, ref);

log(`arms flipped ${a.armsFlipped}, legs flipped ${a.legsFlipped}, facing conflict ${a.inconsistent}`);
log(`dropped: ${pose.dropped.length ? pose.dropped.join(', ') : 'none'}`);
if (a.weak.length) log(`low visibility: ${a.weak.join(', ')}`);
table([{
    spineDeg: +pose.spineDeg.toFixed(1), neckDeg: pose.neckDeg === undefined ? '-' : +pose.neckDeg.toFixed(1),
    shoulderTilt: +pose.shoulderTiltDeg.toFixed(1), pelvicTilt: +pose.pelvicTiltDeg.toFixed(1)
}]);
const limbRows = [];
for (const k of ['leftArm', 'rightArm', 'leftLeg', 'rightLeg'])
    limbRows.push(pose[k]
        ? { limb: k, root: +(pose[k].shoulderDeg !== undefined ? pose[k].shoulderDeg : pose[k].hipDeg).toFixed(1),
            bend: +(pose[k].elbowDeg !== undefined ? pose[k].elbowDeg : pose[k].kneeDeg).toFixed(1) }
        : { limb: k, root: 'DROPPED', bend: '-' });
table(limbRows);

const img = Skia.Image.load(PHOTO);
const PW = img.width, PH = img.height;

// Anchor the canon on the TORSO, which is the most rigid thing both figures have. Limb LENGTHS
// will still disagree - Bolt is not built to an 8-head canon - so judge the overlay by where the
// limbs POINT, which is the only thing the mapping recovers.
//
// Measured off a TRIAL FIGURE rather than from the canon's constants. The arithmetic version -
// shoulder line at 1.4H, pelvis at 3.6H, so torso = 2.2H, anchored on the shoulder midpoint - is
// what this did first and it put the head off the shoulder: `originX` is the axis of the UNPOSED
// figure, and `spineDeg` then rotates the whole upper body about `pelvis.center`, so neither the
// shoulder midpoint nor the projected torso length is where the constants say. Asking a built
// figure where its own shoulders and hips ended up sidesteps all of that.
const opts = { shoulderTiltDeg: pose.shoulderTiltDeg, pelvicTiltDeg: pose.pelvicTiltDeg, pose: pose };
const shoulderMid = mid(a.joints.leftShoulder, a.joints.rightShoulder);
const hipMid = mid(a.joints.leftHip, a.joints.rightHip);
const dist = (p, q) => Math.hypot(p.x - q.x, p.y - q.y);

const trial = Drawing.createMannequinFigure(0, 0, 1000, opts);
const trialTorso = dist(mid(trial.clavicles.left, trial.clavicles.right),
                        mid(trial.pelvis.leftHip, trial.pelvis.rightHip));
const totalHeight = 1000 * (dist(shoulderMid, hipMid) / trialTorso);

// The pelvis is the spine's pivot and so is the one landmark the lean does not move. Placement is
// linear in the origin, so one trial at the final scale gives the offset exactly.
const at0 = Drawing.createMannequinFigure(0, 0, totalHeight, opts);
const h0 = mid(at0.pelvis.leftHip, at0.pelvis.rightHip);

function placed(ox, oy) {
    return Drawing.createMannequinFigure(
        hipMid.x - h0.x + ox, hipMid.y - h0.y + oy, totalHeight, opts);
}

const GAP = 16, LABEL = 30;
const canvas = createCanvas(PW * 3 + GAP * 4, PH + LABEL + GAP * 2);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, canvas.width, canvas.height);
ctx.textBaseline = 'top';

const BONES = [['leftShoulder', 'rightShoulder'], ['leftHip', 'rightHip'],
               ['leftShoulder', 'leftHip'], ['rightShoulder', 'rightHip'],
               ['leftShoulder', 'leftElbow'], ['leftElbow', 'leftWrist'],
               ['rightShoulder', 'rightElbow'], ['rightElbow', 'rightWrist'],
               ['leftHip', 'leftKnee'], ['leftKnee', 'leftAnkle'],
               ['rightHip', 'rightKnee'], ['rightKnee', 'rightAnkle']];

for (let panel = 0; panel < 3; panel++) {
    const x = GAP + panel * (PW + GAP), y = GAP + LABEL;
    ctx.save();
    const frame = new CanvasPath();
    frame.rect(x, y, PW, PH);
    ctx.clip(frame);

    if (panel < 2) {
        ctx.drawImage(img, x, y);
    } else {
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(x, y, PW, PH);
    }

    if (panel === 0) {
        // What the detector saw, in its own landmarks - not through the mapping at all.
        ctx.strokeStyle = '#39d353';
        ctx.lineWidth = 3;
        for (const [p, q] of BONES) {
            ctx.beginPath();
            ctx.moveTo(a.joints[p].x + x, a.joints[p].y + y);
            ctx.lineTo(a.joints[q].x + x, a.joints[q].y + y);
            ctx.stroke();
        }
        // Low-visibility joints marked hollow: the detector guessing, flagged rather than hidden.
        for (const k of Object.keys(a.joints)) {
            const p = a.joints[k];
            if (p.v === undefined) continue;
            ctx.beginPath();
            ctx.arc(p.x + x, p.y + y, 5, 0, Math.PI * 2);
            if (p.v >= 0.5) { ctx.fillStyle = '#39d353'; ctx.fill(); }
            else { ctx.strokeStyle = '#e5484d'; ctx.lineWidth = 2.5; ctx.stroke(); }
        }
    } else {
        const fig = placed(x, y);
        ctx.drawMannequin(fig, false, {
            blueLineColor: panel === 1 ? '#ffd166' : '#9aa4b0',
            graphiteColor: panel === 1 ? '#c9553d' : '#2b3038',
            lineWidth: panel === 1 ? 3 : 4
        });
    }
    ctx.restore();

    ctx.strokeStyle = '#c8c2b6';
    ctx.lineWidth = 1;
    ctx.strokeRect(x, y, PW, PH);
    ctx.fillStyle = '#1c2733';
    ctx.font = '600 13px sans-serif';
    ctx.fillText(['1. what the detector saw', '2. recovered pose, overlaid',
                  '3. the mannequin alone'][panel], x, GAP + 4);
    ctx.font = '400 11px sans-serif';
    ctx.fillStyle = '#7a7068';
    ctx.fillText(['green = confident, hollow red = below 0.5',
                  'scaled on the torso; limb lengths are the canon’s',
                  'what a director would be handed'][panel], x, GAP + 18);
}
canvas;
