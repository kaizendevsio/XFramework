// Import the camera's YUV surface instead of uploading an RGBA texture for each frame.
// External textures apply the source's crop/orientation and color conversion in the shader.
// This remains a capability probe: WebGPU or canvas -> VideoFrame can be unavailable.
export class GpuFramePainter {
    static async create(gpu = globalThis.navigator?.gpu) {
        if (!gpu) return null;
        let device;
        try {
            const adapter = await gpu.requestAdapter({ powerPreference: 'low-power' });
            if (!adapter) return null;
            device = await adapter.requestDevice();
            const canvas = new OffscreenCanvas(1, 1), context = canvas.getContext('webgpu');
            if (!context) throw Error('no-webgpu-canvas');
            const format = gpu.getPreferredCanvasFormat();
            const module = device.createShaderModule({ code: `
                struct Vertex { @builtin(position) position: vec4f, @location(0) uv: vec2f }
                @vertex fn vertex(@builtin(vertex_index) i: u32) -> Vertex {
                    let p = array(vec2f(-1., -1.), vec2f(3., -1.), vec2f(-1., 3.));
                    var v: Vertex;
                    v.position = vec4f(p[i], 0., 1.);
                    v.uv = vec2f(p[i].x * .5 + .5, .5 - p[i].y * .5);
                    return v;
                }
                @group(0) @binding(0) var s: sampler;
                @group(0) @binding(1) var camera: texture_external;
                @fragment fn fragment(v: Vertex) -> @location(0) vec4f {
                    return vec4f(textureSampleBaseClampToEdge(camera, s, v.uv).rgb, 1.);
                }` });
            const pipeline = await device.createRenderPipelineAsync({ layout: 'auto',
                vertex: { module, entryPoint: 'vertex' },
                fragment: { module, entryPoint: 'fragment', targets: [{ format }] } });
            context.configure({ device, format, alphaMode: 'opaque' });
            return new GpuFramePainter(device, canvas, context, pipeline);
        } catch { device?.destroy(); return null; }
    }

    constructor(device, canvas, context, pipeline) {
        Object.assign(this, { device, canvas, context, pipeline, checked: false, failed: false });
        this.sampler = device.createSampler({ magFilter: 'linear', minFilter: 'linear' });
        device.lost.then(() => { this.failed = true; });
        device.addEventListener('uncapturederror', event => { event.preventDefault(); this.failed = true; });
    }

    paint(frame, width, height) {
        if (this.failed) throw Error('webgpu-lost');
        const { canvas, device, context, pipeline } = this;
        if (canvas.width !== width) canvas.width = width;
        if (canvas.height !== height) canvas.height = height;
        const bindings = device.createBindGroup({ layout: pipeline.getBindGroupLayout(0), entries: [
            { binding: 0, resource: this.sampler },
            { binding: 1, resource: device.importExternalTexture({ source: frame }) }
        ] });
        const commands = device.createCommandEncoder();
        const pass = commands.beginRenderPass({ colorAttachments: [{
            view: context.getCurrentTexture().createView(), loadOp: 'clear', storeOp: 'store',
            clearValue: { r: 0, g: 0, b: 0, a: 1 }
        }] });
        pass.setPipeline(pipeline); pass.setBindGroup(0, bindings); pass.draw(3); pass.end();
        device.queue.submit([commands.finish()]);
        return canvas;
    }

    release() {
        this.failed = true;
        this.context.unconfigure();
        this.device.destroy();
        this.canvas.width = this.canvas.height = 0;
    }
}
