// Procedural water shader with directional surge effect
shader_type spatial;

uniform vec4 water_color : source_color = vec4(0.0, 0.42, 0.72, 0.88);
uniform vec4 deep_color : source_color = vec4(0.0, 0.1, 0.32, 1.0);
uniform float wave_speed : hint_range(0.0, 5.0) = 1.0;
uniform float wave_height : hint_range(0.0, 3.0) = 1.2;
uniform float fresnel_power : hint_range(0.1, 10.0) = 3.0;
uniform float specular_intensity : hint_range(0.0, 2.0) = 0.8;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float roughness : hint_range(0.0, 1.0) = 0.15;

// Directional surge control
uniform float surge_angle : hint_range(-3.1416, 3.1416) = 0.3;
uniform float surge_intensity : hint_range(0.0, 3.0) = 1.2;

// Ripple interaction — filled by GDScript
uniform vec4 ripple_data[32];
uniform float ripple_speed = 4.0;

varying vec3 world_pos;

vec3 mod289_v3(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec2 mod289_v2(vec2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec3 permute(vec3 x) { return mod289_v3(((x * 34.0) + 1.0) * x); }

float snoise(vec2 v) {
    const vec4 C = vec4(0.211324865405187, 0.366025403784439,
                       -0.577350269189626, 0.024390243902439);
    vec2 i  = floor(v + dot(v, C.yy));
    vec2 x0 = v - i + dot(i, C.xx);
    vec2 i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
    vec4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;
    i = mod289_v2(i);
    vec3 p = permute(permute(i.y + vec3(0.0, i1.y, 1.0))
        + i.x + vec3(0.0, i1.x, 1.0));
    vec3 m = max(0.5 - vec3(dot(x0,x0), dot(x12.xy,x12.xy),
                            dot(x12.zw,x12.zw)), 0.0);
    m = m * m;
    m = m * m;
    vec3 x = 2.0 * fract(p * C.www) - 1.0;
    vec3 h = abs(x) - 0.5;
    vec3 ox = floor(x + 0.5);
    vec3 a0 = x - ox;
    m *= 1.79284291400159 - 0.85373472095314 * (a0*a0 + h*h);
    vec3 g;
    g.x  = a0.x  * x0.x  + h.x  * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 130.0 * dot(m, g);
}

float fbm(vec2 p, int octaves) {
    float val = 0.0;
    float amp = 0.5;
    mat2 rot = mat2(vec2(0.866, 0.5), vec2(-0.5, 0.866));
    for (int i = 0; i < octaves; i++) {
        val += amp * (0.5 + 0.5 * snoise(p));
        p = rot * p * 2.03;
        amp *= 0.5;
    }
    return val;
}

// --- Directional surge wave (noise-driven, no planar sine waves) ---
// Uses noise domain advection + wave-steepening to create irregular,
// organic-looking surge that moves in a preferred direction.
float sample_surge(vec2 xz, float t) {
    float angle = surge_angle;
    vec2 dir = vec2(cos(angle), sin(angle));

    // Advect the sampling position in the surge direction so waves
    // appear to travel that way. Multiple layers at different speeds
    // and scales for natural irregularity.
    vec2 base_uv = xz * 0.14 - dir * t * 0.10;

    // Layer 1: large-scale swell — pure noise displacement
    float n1 = snoise(base_uv + vec2(t * 0.04, 0.0)) * 0.5 + 0.5;

    // Layer 2: mid-scale waves — advected faster
    float n2 = snoise(xz * 0.25 - dir * t * 0.20 + vec2(1.7, 3.1)) * 0.5 + 0.5;

    // Layer 3: smaller chop — advected even faster, different pattern
    float n3 = snoise(xz * 0.50 - dir * t * 0.35 + vec2(5.3, 2.7)) * 0.5 + 0.5;

    // Domain warp: use noise to displace coordinates for layer 2 & 3,
    // breaking any residual regularity
    vec2 warp = vec2(
        snoise(xz * 0.08 + vec2(t * 0.02)) * 0.5,
        snoise(xz * 0.08 + vec2(t * 0.02) + vec2(10.0)) * 0.5
    );
    float n2w = snoise((xz + warp * 4.0) * 0.25 - dir * t * 0.20 + vec2(1.7, 3.1)) * 0.5 + 0.5;
    float n3w = snoise((xz + warp * 2.5) * 0.50 - dir * t * 0.35 + vec2(5.3, 2.7)) * 0.5 + 0.5;

    // Wave-steepening: gentle nonlinear transform for natural wave asymmetry.
    float combine = n1 * 0.5 + n2w * 0.3 + n3w * 0.2;
    // Single smoothstep — enough for crest sharpness without rubbery look
    float steep = combine * combine * (3.0 - 2.0 * combine);
    steep = mix(combine, steep, 0.25);

    // Center around 0
    float h = (steep - 0.5) * 2.0 * surge_intensity;

    // Quick capillary ripple overlay — tiny high-frequency noise that moves fast,
    // breaks the "sticky noise blob" look and gives the surface a watery sparkle
    float cap = snoise(xz * 1.2 - dir * t * 0.6 + vec2(9.7, 4.3)) * 0.5 + 0.5;
    cap = (cap - 0.5) * surge_intensity * 0.15;
    h += cap;

    return h;
}

float sample_noise_height(vec2 xz, float t) {
    float h = 0.0;

    // Low-frequency ambient ripple
    vec2 p = xz * 0.10;
    h += fbm(p + vec2(t * 0.02, t * 0.015), 3) * 0.2;

    // Mid-frequency surface detail
    h += fbm(p * 3.0 + vec2(t * -0.04, t * 0.03), 2) * 0.1;

    // High-frequency chop
    h += fbm(p * 10.0 + vec2(t * 0.1, t * -0.08), 1) * 0.04;

    return (h - 0.15) * wave_height;
}

float sample_ripple_height(vec2 xz) {
    float h = 0.0;
    for (int i = 0; i < 32; i++) {
        vec4 r = ripple_data[i];
        if (r.w <= 0.001) continue;
        vec2 center = r.xy;
        float elapsed = r.z;
        float intensity = r.w;
        float dist = length(xz - center);
        float radius = elapsed * ripple_speed;
        float ring_width = 2.0 + radius * 0.18;
        float d = abs(dist - radius);
        float ring_mask = 1.0 - smoothstep(0.0, ring_width, d);
        if (ring_mask <= 0.0) continue;
        float wave = sin(dist * 2.5 - elapsed * 8.0);
        h += wave * intensity * ring_mask * 0.08;
    }
    return h;
}

float sample_total_height(vec2 xz, float t) {
    return sample_noise_height(xz, t) + sample_surge(xz, t) + sample_ripple_height(xz);
}

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float t = TIME * wave_speed;
    float surge_h = sample_surge(world_pos.xz, t);
    float noise_h = sample_noise_height(world_pos.xz, t);
    float ripple_h = sample_ripple_height(world_pos.xz);
    VERTEX.y += surge_h + noise_h + ripple_h;

    float e = 0.08;
    float hL = sample_total_height(world_pos.xz + vec2(-e, 0.0), t);
    float hR = sample_total_height(world_pos.xz + vec2( e, 0.0), t);
    float hU = sample_total_height(world_pos.xz + vec2(0.0, -e), t);
    float hD = sample_total_height(world_pos.xz + vec2(0.0,  e), t);
    NORMAL = normalize(vec3(hL - hR, e * 2.0, hU - hD));
}

void fragment() {
    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - world_pos);
    float fresnel = pow(1.0 - max(dot(NORMAL, view_dir), 0.0), fresnel_power);
    vec4 final_color = mix(deep_color, water_color, fresnel);
    ALBEDO = final_color.rgb;
    ALPHA = final_color.a;
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = specular_intensity;
}
