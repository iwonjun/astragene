shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
varying vec4 data;
void vertex(){data=INSTANCE_CUSTOM; MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0],INV_VIEW_MATRIX[1],INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);}
void fragment(){
 ALBEDO=UV.x<data.a?data.rgb:vec3(0.05,0.035,0.08);
 ALPHA=0.92;
}
