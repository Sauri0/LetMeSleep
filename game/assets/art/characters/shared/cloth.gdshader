shader_type spatial;
render_mode diffuse_burley;
uniform vec4 primary_color : source_color = vec4(0.47,0.58,0.71,1.0);
uniform vec4 accent_color : source_color = vec4(0.88,0.87,0.79,1.0);
uniform int pattern = 0;
void fragment(){
    float band = smoothstep(0.82,0.85,fract(UV.x*16.0));
    vec3 stripe = mix(vec3(0.92,0.93,0.89),accent_color.rgb,0.20);
    vec3 color = mix(primary_color.rgb,stripe,band*0.83);
    if(pattern==1){
        float across = smoothstep(0.76,0.80,fract(UV.y*14.0));
        color = mix(primary_color.rgb,stripe,across*0.65);
    }else if(pattern==2){
        color = primary_color.rgb*0.82;
    }
    ALBEDO = color;
    ROUGHNESS = 0.82;
}
