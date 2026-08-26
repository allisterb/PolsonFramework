# Studio Manual 08: Full-Body Anatomy, Mannequins & Facial Expressions

> **Source Reference**: *Imaginative Drawing*, Chapter 4: "Anatomy" (`reference/books/chapter4_anatomy.pdf`)  
> **Purpose**: Translates human anatomical construction (8-head proportional canon, 3 primary solid masses, dynamic contrapposto spine curves, volumetric mannequin blocking, upper-torso muscle landmarks, and the 6 universal facial expressions) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. The 8-Head Proportional Canon & 3 Primary Masses

> **Core Insight from the Book (Page 350 & 550)**:
> In classical figure drawing and heroic illustration, the human figure is divided into **8 equal Head Units ($H$)**:

```
 0.0 H ─── Top of Head (Crown)
 1.0 H ─── Chin & Jaw Base
 2.0 H ─── Nipples & Mid-Chest
 3.0 H ─── Navel & Elbows
 4.0 H ─── Pubic Bone (Crotch) & Wrists ◄── Exact Center of Figure Height!
 5.0 H ─── Mid-Thigh
 6.0 H ─── Bottom of Knees (Patella)
 7.0 H ─── Mid-Calf / Lower Shin
 8.0 H ─── Soles of Feet (Ground Line)
```

### The 3 Solid Masses & Dynamic Contrapposto
The human torso is NOT a rigid monolith. It consists of **3 solid masses** connected by the flexible **Vertebral Column (Spine S-Curve)**:
1. **Head (Solid Egg/Box, $1.0 H$)**: Tilts with cervical neck vertebrae.
2. **Ribcage (Thorax Egg, $1.5 H$)**: Houses heart/lungs, tilts back and laterally.
3. **Pelvis (Solid Basin/Box, $1.0 H$)**: Tilts in opposition to the ribcage (**Contrapposto**), transferring weight onto the active standing leg.

---

## 2. The Volumetric Mannequin Model

> **Core Insight from the Book (Page 550–551)**:
> Rather than drawing surface contours directly, professional artists block the figure using **simplified volumetric primitives**:
> - **Torso**: Cranial sphere, ribcage egg, and pelvic basin.
> - **Limbs**: Tapered cylinders for upper arms/forearms and thighs/calves.
> - **Joints**: Spherical mechanical hinges at shoulders, elbows, hips, and knees.
> - **Terminals**: Wedge boxes for hands and feet.

---

## 3. Upper-Torso Muscle Landmarks

> **Core Insight from the Book (Section 4.4, Pages 427–453)**:
> When detailing the torso over the mannequin foundation, 5 muscle landmarks define the silhouette:
> 1. **Clavicles (Collarbones)**: S-curved handlebars connecting the sternal notch to the shoulder caps.
> 2. **Deltoids (Shoulder Caps)**: Inverted teardrop muscles wrapping around the upper arm.
> 3. **Pectoralis Major (Chest Plates)**: Square/fan-shaped plates inserting directly into the humerus bone.
> 4. **Sternocleidomastoid (Neck V-Tendons)**: Prominent diagonal cords running from the mastoid process behind the ear down to the sternum.
> 5. **Rectus Abdominis (Core 6-Pack Grid)**: Divided into 3 horizontal tiers by tendinous inscriptions.

---

## 4. The 6 Universal Facial Muscle Expressions

> **Core Insight from the Book (Section 4.4, Pages 464–474)**:
> All complex emotional expressions decompose into 6 universal muscular activation patterns:
>
> 1. **`"joy"`**: Zygomaticus major contracts $\implies$ mouth corners pull up & out; Orbicularis oculi contracts $\implies$ lower eyelids push up, crinkling crow's feet.
> 2. **`"anger"`**: Corrugator supercilii contracts $\implies$ eyebrows pull sharply down and inward into a fierce V-shape; eyes narrow; mouth squares.
> 3. **`"fear"`**: Frontalis contracts $\implies$ inner and outer eyebrows raise high and flatten; eyes pop wide with upper sclera visible; mouth drops open.
> 4. **`"sadness"`**: Frontalis (medial) contracts while Corrugator relaxes $\implies$ inner eyebrow tips pull up into an inverted peak ($\land$ shape); mouth corners pull down (Depressor anguli oris).
> 5. **`"surprise"`**: Eyebrows arch high in uniform curves; eyes widen in circles; jaw drops open into a relaxed vertical oval.
> 6. **`"disgust"`**: Levator labii superioris contracts $\implies$ upper lip curls upward in a sneer, wrinkling the bridge of the nose; eyebrows lower slightly.
