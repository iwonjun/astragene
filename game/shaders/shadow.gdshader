shader_type spatial;
render_mode unshaded,cull_disabled,depth_draw_never;
varying vec4 data;
void vertex(){data=INSTANCE_CUSTOM;}
void fragment(){ALBEDO=vec3(0.02,0.015,0.045); ALPHA=(1.0-smoothstep(0.25,0.5,length(UV-0.5)))*data.a*0.4;}
