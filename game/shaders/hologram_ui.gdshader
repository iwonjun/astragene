shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform vec4 accent : source_color = vec4(0.14,0.78,0.96,1.0);
void fragment(){
 vec2 p=UV;float cut=0.035;
 if(p.x+p.y<cut || (1.0-p.x)+(1.0-p.y)<cut)discard;
 vec3 behind=textureLod(screen_texture,SCREEN_UV,2.5).rgb;
 float edge=step(p.x,0.004)+step(p.y,0.006)+step(0.996,p.x)+step(0.994,p.y);
 float scan=sin(FRAGCOORD.y*2.8)*0.007;
 COLOR=vec4(mix(vec3(0.025,0.045,0.085),behind,0.12)+accent.rgb*clamp(edge,0.0,1.0)*0.65+scan,0.96);
}
