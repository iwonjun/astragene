shader_type spatial;
render_mode cull_front, unshaded;
void vertex() {
 float depth = length((MODELVIEW_MATRIX * vec4(VERTEX,1.0)).xyz);
 VERTEX += NORMAL * clamp(depth * 0.0007,0.014,0.035);
}
void fragment(){ ALBEDO=vec3(0.045,0.035,0.085); }
