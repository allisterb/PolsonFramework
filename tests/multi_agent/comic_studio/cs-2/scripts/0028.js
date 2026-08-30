Stage.begin('Critic');
Stage.note('Probing the SkSL entry-point signature and uniform marshalling before committing the cape to a shader — a shader that silently compiles to black would be a much worse defect than the gradient it replaces.');

const canvas = createCanvas(300, 120);
const ctx = canvas.getContext('2d');
try {
    const sh = Skia.Shader.sksl(`
        uniform float2 u_a;
        uniform float4 u_c1;
        uniform float4 u_c2;
        half4 main(float2 coord) {
            float t = clamp((coord.x - u_a.x) / (u_a.y - u_a.x), 0.0, 1.0);
            float4 c = mix(u_c1, u_c2, t);
            return half4(c);
        }`,
        { u_a: [0, 300], u_c1: [0.9, 0.25, 0.2, 1.0], u_c2: [0.2, 0.55, 0.95, 1.0] });
    ctx.fillStyle = sh;
    ctx.fillRect(0, 0, 300, 120);
    log('sksl ok, shader type: ' + typeof sh);
} catch (e) {
    error('sksl failed: ' + e.message);
    ctx.fillStyle = '#000'; ctx.fillRect(0, 0, 300, 120);
}
log('probe pixel(20,60) and (280,60) after render are checked by re-reading the file next call');
canvas;
