shader_type spatial;
render_mode diffuse_toon, specular_disabled;
varying vec4 team;
varying vec3 local_position;
void vertex(){ team=INSTANCE_CUSTOM; local_position=VERTEX; }
void fragment(){
 if(team.a>=0.0 && fract(sin(dot(floor(local_position*20.0),vec3(12.9,78.2,35.4)))*43758.5)>team.a)discard;
 ALBEDO=COLOR.rgb;
 float rim=pow(1.0-max(dot(NORMAL,VIEW),0.0),3.0);
 EMISSION=team.rgb*(COLOR.a*1.6+rim*0.45);
 if(team.a<0.0){ ALBEDO=vec3(1.0); EMISSION=vec3(1.5); }
 ROUGHNESS=0.9;
}
void light(){
 float n=dot(NORMAL,LIGHT);
 float band=0.27+0.30*smoothstep(0.05,0.14,n)+0.43*smoothstep(0.55,0.64,n);
 DIFFUSE_LIGHT+=ALBEDO*mix(vec3(0.58,0.52,0.72),vec3(1.0),band)*LIGHT_COLOR*ATTENUATION/3.14159265;
}
