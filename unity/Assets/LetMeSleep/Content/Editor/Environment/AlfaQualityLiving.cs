using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Content.Editor
{
    public static partial class AlfaMapBuilder
    {
        static void BuildQualityLiving(GameObject house)
        {
            QualityMaterials();QualityLampMaterials();
            var quality=Child(house.transform.Find("HouseDressing"),"QualityLiving");
            BuildQualityLivingRug(F(house,"Living_Rug"));
            BuildQualitySofa(F(house,"Living_Sofa"));BuildQualityCoffeeTable(F(house,"Living_Table"));BuildQualityShelf(F(house,"Living_Shelf"));
            var textiles=F(house,"Living_Sofa_Textiles");foreach(Transform child in textiles.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach(float x in new[]{-.57f,.57f})QualityPart(textiles,"Cushion_"+Token(x),new Vector3(x,.735f,.17f),QualityPillow(new Vector3(.38f,.32f,.16f)),x<0?"Quality_Blue":"Quality_Linen","Quality_Thread");
            QualityPart(textiles,"Seat_Throw",Vector3.zero,QualityThrow(),"Quality_Linen","Quality_Blue");
            BuildQualityBooks(F(house,"Living_Shelf_Books"));BuildQualityWindow(house,quality);BuildQualityLivingDoor(house);
            BuildQualityFloorLamp(quality,new Vector3(.65f,0,3.85f));BuildQualityWallDetails(quality);
            BuildQualityCeilingFixture(F(house,"CeilingFixture_Living"));
            ExportQualityLiving(house);
        }

        static void BuildQualityLivingRug(Transform rug)
        {
            RemoveQualityReplacedVisuals(rug);
            Func<float,float,float,float,QualityMesh> layer=(width,depth,bottom,top)=>{
                var mesh=new QualityMesh();float x=width*.5f,z=depth*.5f,r=.025f;
                var points=new[]{new Vector2(-x+r,-z),new Vector2(x-r,-z),new Vector2(x,-z+r),new Vector2(x,z-r),new Vector2(x-r,z),new Vector2(-x+r,z),new Vector2(-x,z-r),new Vector2(-x,-z+r)};
                for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];
                    mesh.Triangle(new Vector3(0,top,0),new Vector3(a.x,top,a.y),new Vector3(b.x,top,b.y),Vector3.up);
                    mesh.Triangle(new Vector3(0,bottom,0),new Vector3(b.x,bottom,b.y),new Vector3(a.x,bottom,a.y),Vector3.down);
                    mesh.Quad(new Vector3(a.x,bottom,a.y),new Vector3(b.x,bottom,b.y),new Vector3(b.x,top,b.y),new Vector3(a.x,top,a.y),new Vector3(a.x+b.x,0,a.y+b.y));
                }return mesh;
            };
            var center=new Vector3(1.9f,0,2.35f);
            QualityPart(rug,"Woven_Backing",center,layer(2.45f,2.4f,0,.006f),"Quality_Rust");
            QualityPart(rug,"Linen_Bound_Edge",center,layer(2.25f,2.20f,.006f,.007f),"Quality_Linen");
            QualityPart(rug,"Rust_Woven_Field",center,layer(2.13f,2.08f,.007f,.008f),"Quality_Rust");
            foreach(float z in new[]{1.51f,3.19f})QualityTimber(rug,"Blue_Woven_Band_"+Token(z),new Vector3(1.9f,.0085f,z),new Vector3(1.94f,.001f,.045f),"Quality_Blue",.0003f);
        }

        static void BuildQualitySofa(Transform sofa)
        {
            RemoveQualityReplacedVisuals(sofa);var parts=Child(sofa,"Crafted_FrameAndUpholstery");
            // Exposed wooden chassis, four feet, distinct arm pads and two sewn seat pads.
            foreach(float x in new[]{-.92f,.92f})foreach(float z in new[]{-.30f,.30f}){
                // With the authored -90-degree sofa rotation, the rear feet are on
                // bare floor; only the front pair stands on the .008 m rug field.
                float support=z>0?0:.008f;
                QualityBoxCollider(QualityTimber(parts,"Foot_"+Token(x)+"_"+Token(z),new Vector3(x,support+.109f,z),new Vector3(.12f,.218f,.12f),"Quality_WoodEnd",.013f,.72f));
            }
            QualityTimber(parts,"Front_Apron",new Vector3(0,.315f,-.37f),new Vector3(1.96f,.20f,.10f),"Quality_Wood",.014f);
            QualityTimber(parts,"Rear_Rail",new Vector3(0,.38f,.35f),new Vector3(1.94f,.16f,.09f),"Quality_WoodEnd",.012f);
            foreach(float x in new[]{-.97f,.97f}){
                QualityTimber(parts,"Arm_Frame_"+Token(x),new Vector3(x,.51f,-.015f),new Vector3(.18f,.45f,.80f),"Quality_Wood",.022f);
                QualityPart(parts,"Arm_Pad_"+Token(x),new Vector3(x,.70f,-.015f),QualityPillow(new Vector3(.20f,.15f,.78f),true),"Quality_Rust","Quality_Thread");
                foreach(float z in new[]{-.32f,.30f}){
                    var peg=QualityPart(parts,"Joinery_Peg_"+Token(x)+"_"+Token(z),new Vector3(x+(x<0?-.091f:.091f),.48f,z),QualityLathe(new Vector2(0,0),new Vector2(.012f,0),new Vector2(.012f,.003f),new Vector2(0,.003f)),"Quality_WoodEnd");peg.localRotation=Quaternion.Euler(0,0,x<0?90:-90);
                }
            }
            QualityTimber(parts,"Back_Frame",new Vector3(0,.86f,.355f),new Vector3(2.04f,.62f,.13f),"Quality_Wood",.025f);
            foreach(float x in new[]{-.46f,.46f}){
                QualityPart(parts,"Seat_Pad_"+Token(x),new Vector3(x,.485f,-.04f),QualityPillow(new Vector3(.90f,.18f,.72f),true),"Quality_Rust","Quality_Thread");
                QualityPart(parts,"Back_Pad_"+Token(x),new Vector3(x,.91f,.34f),QualityPillow(new Vector3(.92f,.49f,.15f)),"Quality_Rust","Quality_Thread");
            }
        }

        static void BuildQualityCoffeeTable(Transform table)
        {
            RemoveQualityReplacedVisuals(table);var parts=Child(table,"Crafted_Joinery");
            foreach(float z in new[]{-.234f,0,.234f})QualityTimber(parts,"Top_Plank_"+Token(z),new Vector3(0,.45f,z),new Vector3(1.15f,.06f,.232f),z==0?"Quality_WoodLight":"Quality_Wood",.007f);
            foreach(float x in new[]{-.625f,.625f})QualityTimber(parts,"Breadboard_End_"+Token(x),new Vector3(x,.45f,0),new Vector3(.10f,.06f,.70f),"Quality_Wood",.007f);
            foreach(float x in new[]{-.56f,.56f})foreach(float z in new[]{-.23f,.23f}){
                QualityTimber(parts,"Tapered_Leg_"+Token(x)+"_"+Token(z),new Vector3(x,.214f,z),new Vector3(.07f,.412f,.07f),"Quality_Wood",.007f,.73f);
                var peg=QualityPart(parts,"Peg_"+Token(x)+"_"+Token(z),new Vector3(x,.477f,z),QualityLathe(new Vector2(0,0),new Vector2(.010f,0),new Vector2(.010f,.004f),new Vector2(0,.004f)),"Quality_WoodEnd");
            }
            foreach(float z in new[]{-.23f,.23f})QualityBoxCollider(QualityTimber(parts,"Long_Apron_"+Token(z),new Vector3(0,.376f,z),new Vector3(1.12f,.088f,.045f),"Quality_WoodEnd",.006f));
            foreach(float x in new[]{-.56f,.56f})QualityBoxCollider(QualityTimber(parts,"End_Apron_"+Token(x),new Vector3(x,.376f,0),new Vector3(.045f,.088f,.46f),"Quality_WoodEnd",.006f));
        }

        static void BuildQualityShelf(Transform shelf)
        {
            RemoveQualityReplacedVisuals(shelf);var parts=Child(shelf,"Crafted_Casework");
            foreach(float x in new[]{-.55f,.55f}){
                QualityTimber(parts,"Upright_"+Token(x),new Vector3(x,.85f,0),new Vector3(.10f,1.70f,.35f),"Quality_Wood",.012f);
                QualityTimber(parts,"Foot_Cap_"+Token(x),new Vector3(x,.055f,0),new Vector3(.13f,.11f,.37f),"Quality_WoodEnd",.012f);
            }
            foreach(float y in new[]{.08f,.58f,1.08f,1.65f}){
                QualityTimber(parts,"Shelf_Board_"+Token(y),new Vector3(0,y,0),new Vector3(1.04f,.06f,.35f),"Quality_WoodLight",.006f);
                QualityTimber(parts,"Shelf_Nosing_"+Token(y),new Vector3(0,y-.008f,.177f),new Vector3(1.06f,.042f,.022f),"Quality_Wood",.004f);
            }
            QualityBoxCollider(QualityTimber(parts,"Crown_Overhang",new Vector3(0,1.711f,0),new Vector3(1.26f,.062f,.39f),"Quality_Wood",.011f));
            // Recessed back made of separate thin boards, visible behind the books.
            for(int i=0;i<5;i++)QualityTimber(parts,"Back_Slat_"+i,new Vector3(-.416f+i*.208f,.85f,-.165f),new Vector3(.205f,1.60f,.017f),i%2==0?"Quality_WoodEnd":"Quality_Wood",.002f);
            var storage=Child(parts,"Storage_Basket");storage.localPosition=new Vector3(-.21f,.11f,.015f);
            QualityBoxCollider(QualityTimber(storage,"Basket_Body",new Vector3(0,.16f,0),new Vector3(.42f,.32f,.26f),"Quality_Linen",.025f));
            foreach(float y in new[]{.07f,.15f,.23f,.30f})QualityTimber(storage,"Woven_Seam_"+Token(y),new Vector3(0,y,.13f),new Vector3(.39f,.007f,.004f),"Quality_Thread",.001f);
            QualityTimber(storage,"Handle_Recess",new Vector3(0,.24f,.135f),new Vector3(.11f,.025f,.010f),"Quality_WoodEnd",.006f);
            BuildQualityPlant(parts,new Vector3(-.28f,1.742f,0));
        }

        static void BuildQualityBooks(Transform books)
        {
            foreach(Transform book in books){
                var collider=book.GetComponent<BoxCollider>();Vector3 size=collider.size;RemoveQualityReplacedVisuals(book);
                var parts=Child(book,"Bound_Volume");string cover=book.name.EndsWith("0",StringComparison.Ordinal)?"Quality_Rust":"Quality_Blue";
                QualityTimber(parts,"Paper_Block",new Vector3(0,size.y*.5f,-.006f),new Vector3(size.x-.010f,size.y-.018f,size.z-.022f),"Quality_Linen",.002f);
                foreach(float x in new[]{-size.x*.5f+.0025f,size.x*.5f-.0025f})QualityTimber(parts,"Cover_"+Token(x),new Vector3(x,size.y*.5f,0),new Vector3(.005f,size.y,size.z-.002f),cover,.0015f);
                QualityTimber(parts,"Rounded_Spine",new Vector3(0,size.y*.5f,size.z*.5f-.006f),new Vector3(size.x,size.y-.005f,.012f),cover,.005f);
                foreach(float y in new[]{size.y*.16f,size.y*.80f})QualityTimber(parts,"Spine_Rib_"+Token(y),new Vector3(0,y,size.z*.5f-.0005f),new Vector3(size.x-.009f,.010f,.001f),"Quality_Linen",.0003f);
            }
        }

        static void BuildQualityPlant(Transform parent,Vector3 bottom)
        {
            var plant=Child(parent,"Potted_Leaf_Plant");plant.localPosition=bottom;
            QualityBoxCollider(QualityPart(plant,"Terracotta_Pot",Vector3.zero,QualityLathe(new Vector2(0,0),new Vector2(.09f,0),new Vector2(.115f,.17f),new Vector2(.131f,.18f),new Vector2(.131f,.215f),new Vector2(.112f,.215f),new Vector2(.107f,.185f),new Vector2(.082f,.025f),new Vector2(0,.025f)),"Quality_Terracotta"));
            QualityPart(plant,"Soil",Vector3.zero,QualityLathe(new Vector2(0,.188f),new Vector2(.109f,.188f),new Vector2(.109f,.194f),new Vector2(0,.194f)),"Quality_Soil");
            var leaves=new QualityMesh();
            for(int i=0;i<9;i++){
                float a=i*2.39996f;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-direction.z,0,direction.x);
                float reach=i<3?.12f:.20f,height=i<3?.51f:.35f+(i%3)*.055f;
                var start=new Vector3(0,.195f,0);var stem=start+direction*.04f+Vector3.up*.10f;var middle=start+direction*(reach*.64f)+Vector3.up*(height*.70f);
                var tip=start+direction*reach+Vector3.up*height;var ridge=middle+Vector3.up*.025f;var left=middle-side*.048f;var right=middle+side*.048f;
                leaves.Quad(start-side*.004f,start+side*.004f,stem+side*.004f,stem-side*.004f,-direction);
                foreach(bool back in new[]{false,true}){var normal=back?Vector3.down:Vector3.up;float offset=back?-.003f:0;
                    leaves.Triangle(stem+Vector3.up*offset,left+Vector3.up*offset,ridge+Vector3.up*offset,normal,i%2);
                    leaves.Triangle(left+Vector3.up*offset,tip+Vector3.up*offset,ridge+Vector3.up*offset,normal,i%2);
                    leaves.Triangle(tip+Vector3.up*offset,right+Vector3.up*offset,ridge+Vector3.up*offset,normal,(i+1)%2);
                    leaves.Triangle(right+Vector3.up*offset,stem+Vector3.up*offset,ridge+Vector3.up*offset,normal,(i+1)%2);
                }
            }
            QualityPart(plant,"Ridged_Leaves",Vector3.zero,leaves,"Quality_Leaf","Quality_LeafLight");
        }

        static void BuildQualityWindow(GameObject house,Transform quality)
        {
            var trim=F(house,"RoomCarpentry");
            foreach(Transform item in trim.Cast<Transform>().Where(t=>t.name.StartsWith("Living_Curtain",StringComparison.Ordinal)).ToArray())UnityEngine.Object.DestroyImmediate(item.gameObject);
            foreach(float x in new[]{.65f,2.11f}){
                var curtain=QualityPart(trim,"Living_Curtain_"+Token(x),new Vector3(x,2.285f,.30f),QualityCurtain(.28f,1.12f),"Quality_Rust","Quality_Thread");
                var bounds=curtain.GetComponent<MeshFilter>().sharedMesh.bounds;var collider=curtain.gameObject.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;curtain.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");
            }
            var rod=QualityPart(trim,"Living_CurtainRail",new Vector3(.55f,2.34f,.30f),QualityLathe(new Vector2(0,0),new Vector2(.018f,0),new Vector2(.018f,1.66f),new Vector2(0,1.66f)),"Quality_Iron");rod.localRotation=Quaternion.Euler(0,0,-90);
            foreach(float x in new[]{.50f,2.26f})QualityPart(trim,"Living_Rail_Finial_"+Token(x),new Vector3(x,2.34f,.30f),QualityPillow(new Vector3(.085f,.065f,.065f)),"Quality_WoodEnd","Quality_WoodEnd");
            foreach(float x in new[]{.58f,.72f,2.04f,2.18f})QualityTimber(trim,"Living_Curtain_Tab_"+Token(x),new Vector3(x,2.30f,.30f),new Vector3(.036f,.095f,.047f),"Quality_Rust",.008f);
            // Deep layered surround and actual muntins. The eight original panes and
            // colliders remain intact; this geometry is a visibly separate timber frame.
            foreach(float x in new[]{.7725f,1.9875f})QualityTimber(quality,"Window_Casing_"+Token(x),new Vector3(x,1.70f,.224f),new Vector3(.095f,1.24f,.080f),"Quality_Wood",.009f);
            foreach(float y in new[]{1.105f,2.295f})QualityTimber(quality,"Window_Header_"+Token(y),new Vector3(1.38f,y,.224f),new Vector3(1.31f,.095f,.080f),"Quality_WoodLight",.009f);
            QualityTimber(quality,"Window_Center_Stile",new Vector3(1.38f,1.70f,.197f),new Vector3(.032f,1.00f,.030f),"Quality_Wood",.004f);
            QualityTimber(quality,"Window_Center_Rail",new Vector3(1.38f,1.70f,.197f),new Vector3(1.10f,.030f,.030f),"Quality_Wood",.004f);
        }

        static void BuildQualityLivingDoor(GameObject house)
        {
            var hinge=F(F(house,"LivingDoor").gameObject,"Door_01_Hinge");
            foreach(var filter in hinge.GetComponentsInChildren<MeshFilter>()){
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;var bounds=filter.sharedMesh.bounds;
                bool metal=renderer.sharedMaterials.Any(m=>m.name=="Iron");var shape=QualityRoundedBox(bounds.size,metal?.004f:.007f);
                // Preserve the proven transform, pivot, leaf dimensions and mobile collider.
                for(int i=0;i<shape.vertices.Count;i++)shape.vertices[i]+=bounds.center;
                filter.sharedMesh=shape.Save("LivingDoor_"+filter.name,1);renderer.sharedMaterial=materials[metal?"Quality_Iron":filter.name.Contains("Panel")?"Quality_WoodEnd":"Quality_Wood"];
            }
            var pegs=Child(hinge,"Crafted_Hardware");
            foreach(float z in new[]{-.041f,.041f})foreach(float x in new[]{.10f,.97f})foreach(float y in new[]{.14f,2.055f}){
                var peg=QualityPart(pegs,"Frame_Peg_"+Token(x)+"_"+Token(y)+"_"+Token(z),new Vector3(x,y,z),QualityLathe(new Vector2(0,0),new Vector2(.012f,0),new Vector2(.012f,.003f),new Vector2(0,.003f)),"Quality_WoodEnd");peg.localRotation=Quaternion.Euler(z>0?90:-90,0,0);
            }
        }

        static void QualityLampMaterials()
        {
            foreach(bool inner in new[]{false,true}){
                string name=inner?"Quality_LampShadeInner":"Quality_LampShade";
                MakeQualityMaterial(name,inner?new Color(.88f,.72f,.50f):new Color(.72f,.58f,.42f),.88f);
                var material=materials[name];material.SetColor("_EmissionColor",inner?new Color(.20f,.10f,.03f):new Color(.14f,.07f,.02f));material.EnableKeyword("_EMISSION");
                material.SetFloat("_SpecularHighlights",0);material.SetFloat("_EnvironmentReflections",0);material.SetFloat("_ReceiveShadows",0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");material.EnableKeyword("_RECEIVE_SHADOWS_OFF");material.SetShaderPassEnabled("ShadowCaster",false);EditorUtility.SetDirty(material);
            }
        }

        static void BuildQualityFloorLamp(Transform parent,Vector3 position)
        {
            var lamp=Child(parent,"Standing_Lamp");lamp.localPosition=position;
            QualityPart(lamp,"Weighted_Base",Vector3.zero,QualityLathe(new Vector2(0,0),new Vector2(.17f,0),new Vector2(.18f,.026f),new Vector2(.16f,.06f),new Vector2(.065f,.078f),new Vector2(0,.078f)),"Quality_WoodEnd");
            QualityPart(lamp,"Turned_Stem",Vector3.zero,QualityLathe(new Vector2(0,.06f),new Vector2(.035f,.06f),new Vector2(.025f,.25f),new Vector2(.026f,1.25f),new Vector2(.07f,1.28f),new Vector2(0,1.28f)),"Quality_Wood");
            QualityPart(lamp,"Shade_Fabric",Vector3.zero,QualityLathe(new Vector2(.27f,1.28f),new Vector2(.16f,1.67f),new Vector2(.151f,1.67f),new Vector2(.261f,1.28f),new Vector2(.27f,1.28f)),"Quality_LampShade");
            foreach(float y in new[]{1.28f,1.67f}){float radius=y<1.5f?.27f:.16f;
                QualityPart(lamp,"Shade_Hem_"+Token(y),Vector3.zero,QualityLathe(new Vector2(radius-.012f,y-.008f),new Vector2(radius+.003f,y-.008f),new Vector2(radius+.003f,y+.008f),new Vector2(radius-.012f,y+.008f),new Vector2(radius-.012f,y-.008f)),"Quality_Linen");}
            QualityPart(lamp,"Frosted_Bulb",new Vector3(0,1.37f,0),QualityPillow(new Vector3(.09f,.14f,.09f)),"Quality_LampShadeInner","Quality_LampShadeInner");
            // No Light or new runtime light anchor: W2 retains the zone lighting contract.
            foreach(var renderer in lamp.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("Shade")||r.name.Contains("Bulb"))){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;}
            foreach(string name in new[]{"Weighted_Base","Turned_Stem","Shade_Fabric"}){
                var part=lamp.Find(name);var collider=part.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=part.GetComponent<MeshFilter>().sharedMesh;part.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");}
        }

        static void BuildQualityWallDetails(Transform parent)
        {
            // Timber cornice meets the ceiling, separating the cream plane from walls.
            foreach(float x in new[]{.22f,4.94f})QualityTimber(parent,"Ceiling_Edge_X_"+Token(x),new Vector3(x,2.75f,2.38f),new Vector3(.08f,.10f,4.40f),"Quality_Wood",.009f);
            foreach(float z in new[]{.22f,4.54f})QualityTimber(parent,"Ceiling_Edge_Z_"+Token(z),new Vector3(2.58f,2.75f,z),new Vector3(4.64f,.10f,.08f),"Quality_Wood",.009f);
            var picture=Child(parent,"Framed_Landscape");picture.localPosition=new Vector3(.205f,1.92f,2.35f);picture.localRotation=Quaternion.Euler(0,90,0);
            QualityTimber(picture,"Backing",Vector3.zero,new Vector3(.88f,.58f,.024f),"Quality_WoodEnd",.003f);
            foreach(float x in new[]{-.43f,.43f})QualityTimber(picture,"Frame_Side_"+Token(x),new Vector3(x,0,.020f),new Vector3(.065f,.65f,.05f),"Quality_Wood",.008f);
            foreach(float y in new[]{-.29f,.29f})QualityTimber(picture,"Frame_Rail_"+Token(y),new Vector3(0,y,.020f),new Vector3(.80f,.065f,.05f),"Quality_WoodLight",.008f);
            var art=new QualityMesh();art.Quad(new Vector3(-.395f,-.255f,.014f),new Vector3(.395f,-.255f,.014f),new Vector3(.395f,.255f,.014f),new Vector3(-.395f,.255f,.014f),Vector3.forward);
            art.Triangle(new Vector3(-.39f,-.25f,.015f),new Vector3(.16f,-.25f,.015f),new Vector3(-.13f,.16f,.015f),Vector3.forward,1);
            art.Triangle(new Vector3(-.10f,-.25f,.016f),new Vector3(.39f,-.25f,.016f),new Vector3(.18f,.07f,.016f),Vector3.forward,2);
            QualityPart(picture,"Original_Hill_Painting",Vector3.zero,art,"Quality_Blue","Quality_Leaf","Quality_LeafLight");
        }

        static void BuildQualityCeilingFixture(Transform fixture)
        {
            foreach(var collider in fixture.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            RemoveQualityReplacedVisuals(fixture);var parts=Child(fixture,"Crafted_Frosted_Glass");
            QualityPart(parts,"Ceiling_Rose",Vector3.zero,QualityLathe(new Vector2(0,-.06f),new Vector2(.13f,-.06f),new Vector2(.18f,-.035f),new Vector2(.18f,0),new Vector2(0,0)),"Quality_Wood");
            QualityPart(parts,"Frosted_Bowl",Vector3.zero,QualityLathe(new Vector2(0,-.19f),new Vector2(.12f,-.18f),new Vector2(.235f,-.12f),new Vector2(.24f,-.075f),new Vector2(.23f,-.075f),new Vector2(.22f,-.11f),new Vector2(.11f,-.169f),new Vector2(0,-.18f)),"Quality_LampShadeInner");
            QualityPart(parts,"Rim",Vector3.zero,QualityLathe(new Vector2(.225f,-.083f),new Vector2(.245f,-.083f),new Vector2(.245f,-.063f),new Vector2(.225f,-.063f),new Vector2(.225f,-.083f)),"Quality_Iron");
            foreach(var renderer in parts.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Frosted_Bowl")){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
            foreach(var filter in parts.GetComponentsInChildren<MeshFilter>()){var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;collider.convex=false;filter.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");}
        }

        static void QualityBoxCollider(Transform part)
        {var bounds=part.GetComponent<MeshFilter>().sharedMesh.bounds;var collider=part.gameObject.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;part.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");}

        [Serializable] class QualityExport {public string schema="lms-quality-meshes-v1",units="metres",axes="Unity +Y/+Z";public QualityExportPart[] parts;}
        [Serializable] class QualityExportPart {public string path;public float[] origin;public float[] vertices,uv;public QualityExportSubmesh[] submeshes;}
        [Serializable] class QualityExportSubmesh {public string material;public float[] color,emission;public float smoothness,metallic;public bool weave;public int[] triangles;}
        static void ExportQualityLiving(GameObject house)
        {
            var roots=new[]{F(house,"Living_Rug"),F(house,"Living_Sofa"),F(house,"Living_Table"),F(house,"Living_Shelf"),F(house,"Living_Shelf_Books"),F(house,"Living_Sofa_Textiles"),F(house,"QualityLiving"),F(house,"LivingDoor"),F(house,"CeilingFixture_Living"),F(house,"RoomCarpentry")};
            var parts=new List<QualityExportPart>();
            foreach(var filter in roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).Distinct()){
                if(Hierarchy(filter.transform).Contains("RoomCarpentry")&&!filter.name.StartsWith("Living",StringComparison.Ordinal))continue;
                var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;if(renderer==null)continue;
                Vector3 origin=house.transform.InverseTransformPoint(filter.transform.position);
                var points=mesh.vertices.Select(v=>house.transform.InverseTransformPoint(filter.transform.TransformPoint(v))-origin).SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();
                var submeshes=new List<QualityExportSubmesh>();for(int i=0;i<mesh.subMeshCount;i++){var material=renderer.sharedMaterials[i];var color=material.GetColor("_BaseColor");var emission=material.GetColor("_EmissionColor");submeshes.Add(new QualityExportSubmesh{material=material.name,color=new[]{color.r,color.g,color.b,color.a},emission=new[]{emission.r,emission.g,emission.b},smoothness=material.GetFloat("_Smoothness"),metallic=material.GetFloat("_Metallic"),weave=material.GetTexture("_BaseMap")!=null&&material.GetTexture("_BaseMap").name=="Quality_LinenWeave",triangles=mesh.GetTriangles(i)});}
                parts.Add(new QualityExportPart{path=Hierarchy(filter.transform),origin=new[]{origin.x,origin.y,origin.z},vertices=points,uv=mesh.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),submeshes=submeshes.ToArray()});
            }
            string repository=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));string directory=Path.Combine(repository,"art_source/unity/environments/quality_living");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"generated_meshes.json"),JsonUtility.ToJson(new QualityExport{parts=parts.ToArray()}));
            File.WriteAllBytes(Path.Combine(directory,"Quality_LinenWeave.png"),AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Materials/Quality_LinenWeave.asset").EncodeToPNG());
        }
    }
}
