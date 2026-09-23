shader_type spatial;
render_mode unshaded, cull_disabled;
uniform sampler2D fog_mask : filter_nearest, repeat_disable;
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
void fragment(){
 float f=texture(fog_mask,world.xz/128.0).r;
 float grid=step(0.97,fract(world.x*0.25))+step(0.97,fract(world.z*0.25));
 ALBEDO=(COLOR.rgb+vec3(grid*0.012))*mix(0.06,1.0,f);
 EMISSION=COLOR.rgb*COLOR.a*0.5*f;
}
