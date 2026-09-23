shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
varying vec4 data;
void vertex(){data=INSTANCE_CUSTOM;}
void fragment(){
 vec2 p=UV-vec2(0.5);float d=length(p);
 float ring=smoothstep(0.34,0.40,d)*(1.0-smoothstep(0.44,0.49,d));
 float opacity=data.a;vec3 tint=data.rgb;
 if(data.a>3.0){
  opacity=data.a-3.0;
  float core=(1.0-smoothstep(0.0,0.28,d))*opacity;
  tint+=vec3(core*2.0);
  tint+=vec3(0.15,0.0,0.25)*sin(d*130.0);
  ring=max(ring,core);
 }else if(data.a>2.0){opacity=data.a-2.0;ring*=smoothstep(-0.3,0.35,p.x)*smoothstep(-0.1,0.1,p.y);}
 ALBEDO=tint;EMISSION=tint*1.3;ALPHA=ring*opacity;
}