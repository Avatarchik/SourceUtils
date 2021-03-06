﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using OpenTK.Graphics.ES20;
using Ziks.WebServer;

namespace SourceUtils.WebExport
{
    internal class MaterialDictionary : ResourceDictionary<MaterialDictionary>
    {
        protected override IEnumerable<string> OnFindResourcePaths( ValveBspFile bsp )
        {
            for ( var i = 0; i < bsp.TextureStringTable.Length; ++i )
            {
                yield return bsp.GetTextureString( i );
            }

            for ( var i = 0; i < bsp.StaticProps.ModelCount; ++i )
            {
                var modelName = bsp.StaticProps.GetModelName( i );
                var mdl = StudioModelFile.FromProvider( modelName, bsp.PakFile, Program.Resources );

                for ( var j = 0; j < mdl.MaterialCount; ++j )
                {
                    yield return mdl.GetMaterialName( j, bsp.PakFile, Program.Resources );
                }
            }
        }

        protected override string NormalizePath( string path )
        {
            path = base.NormalizePath( path );

            if ( !path.StartsWith( "materials/" ) ) path = $"materials/{path}";
            if ( !path.EndsWith( ".vmt" ) ) path = $"{path}.vmt";

            return path;
        }
    }

    public enum MaterialPropertyType
    {
        Boolean = 1,
        Number = 2,
        Color = 3,
        TextureUrl = 4,
        TextureIndex = 5,
        TextureInfo = 6
    }

    public class MaterialProperty
    {
        [JsonProperty( "name" )]
        public string Name { get; set; }

        [JsonProperty( "type" )]
        public MaterialPropertyType Type { get; set; }

        [JsonProperty( "value" )]
        public object Value { get; set; }
    }

    public struct MaterialColor
    {
        public MaterialColor( byte r, byte g, byte b, byte a = 255 )
        {
            R = r / 255f;
            G = g / 255f;
            B = b / 255f;
            A = a / 255f;
        }

        public MaterialColor( Color32 color )
        {
            R = color.R / 255f;
            G = color.G / 255f;
            B = color.B / 255f;
            A = color.A / 255f;
        }

        [JsonProperty("r")]
        public float R { get; set; }
        [JsonProperty("g")]
        public float G { get; set; }
        [JsonProperty("b")]
        public float B { get; set; }
        [JsonProperty("a")]
        public float A { get; set; }
    }

    public class Material
    {
        private static Url GetTextureUrl(string path, string vmtPath, ValveBspFile bsp)
        {
            path = path.ToLower().Replace('\\', '/');
            if (!path.EndsWith(".vtf")) path = $"{path}.vtf";

            path = !path.Contains('/') ? $"{Path.GetDirectoryName(vmtPath)}/{path}" : $"materials/{path}";

            if (bsp != null && bsp.PakFile.ContainsFile(path))
            {
                return $"/maps/{bsp.Name}/{path}.json";
            }

            return $"/{path}.json";
        }

        private static void AddMaterialProperties(Material mat, ValveMaterialFile vmt, string vmtPath, ValveBspFile bsp)
        {
            var shader = vmt.Shaders.First();
            var props = vmt[shader];

            switch (shader.ToLower())
            {
                case "lightmappedgeneric":
                    {
                        mat.Shader = "SourceUtils.Shaders.LightmappedGeneric";
                        break;
                    }
                case "worldvertextransition":
                    {
                        mat.Shader = "SourceUtils.Shaders.Lightmapped2WayBlend";
                        break;
                    }
                case "vertexlitgeneric":
                    {
                        mat.Shader = "SourceUtils.Shaders.VertexLitGeneric";
                        break;
                    }
                case "unlittwotexture":
                case "unlitgeneric":
                    {
                        mat.Shader = "SourceUtils.Shaders.UnlitGeneric";
                        break;
                    }
                case "water":
                    {
                        mat.Shader = "SourceUtils.Shaders.Water";
                        break;
                    }
                default:
                    {
                        mat.Shader = shader;
                        break;
                    }
            }

            foreach (var name in props.PropertyNames)
            {
                switch (name.ToLower())
                {
                    case "$basetexture":
                        mat.SetTextureUrl("basetexture", GetTextureUrl(props[name], vmtPath, bsp));
                        break;
                    case "$hdrcompressedtexture":
                        mat.SetTextureUrl("hdrcompressedtexture", GetTextureUrl(props[name], vmtPath, bsp));
                        break;
                    case "$texture2":
                    case "$basetexture2":
                        mat.SetTextureUrl("basetexture2", GetTextureUrl(props[name], vmtPath, bsp));
                        break;
                    case "$blendmodulatetexture":
                        mat.SetTextureUrl("blendModulateTexture", GetTextureUrl(props[name], vmtPath, bsp));
                        break;
                    case "$normalmap":
                        mat.SetTextureUrl("normalMap", GetTextureUrl(props[name], vmtPath, bsp));
                        break;
                    case "$simpleoverlay":
                        mat.SetTextureUrl("simpleOverlay", GetTextureUrl(props[name], vmtPath, bsp));
                        break;
                    case "$nofog":
                        mat.SetBoolean("fogEnabled", !props.GetBoolean(name));
                        break;
                    case "$alphatest":
                        if (!Program.BaseOptions.Untextured) mat.SetBoolean("alphaTest", props.GetBoolean(name));
                        break;
                    case "$translucent":
                        mat.SetBoolean("translucent", props.GetBoolean(name));
                        break;
                    case "$refract":
                        mat.SetBoolean("refract", props.GetBoolean(name));
                        break;
                    case "$alpha":
                        mat.SetNumber("alpha", props.GetSingle(name));
                        break;
                    case "$nocull":
                        mat.SetBoolean("cullFace", !props.GetBoolean(name));
                        break;
                    case "$notint":
                        mat.SetBoolean("tint", !props.GetBoolean(name));
                        break;
                    case "$blendtintbybasealpha":
                        mat.SetBoolean("baseAlphaTint", props.GetBoolean(name));
                        break;
                    case "$fogstart":
                        mat.SetNumber("fogStart", props.GetSingle(name));
                        break;
                    case "$fogend":
                        mat.SetNumber("fogEnd", props.GetSingle(name));
                        break;
                    case "$fogcolor":
                        mat.SetColor("fogColor", new MaterialColor(props.GetColor(name)));
                        break;
                    case "$reflecttint":
                        mat.SetColor("reflectTint", new MaterialColor(props.GetColor(name)));
                        break;
                    case "$refractamount":
                        mat.SetNumber("refractAmount", props.GetSingle(name));
                        break;
                }
            }
        }

        public static Material Get(ValveBspFile bsp, string path)
        {
            var vmt = bsp == null
                ? ValveMaterialFile.FromProvider(path, Program.Resources)
                : ValveMaterialFile.FromProvider(path, bsp.PakFile, Program.Resources);

            if ( vmt == null ) return null;

            var mat = new Material();

            AddMaterialProperties(mat, vmt, path, bsp);

            return mat;
        }

        public static Material CreateSkyMaterial( ValveBspFile bsp, string skyName )
        {
            var postfixes = new[]
            {
                "rt", "lf", "bk", "ft", "up", "dn"
            };

            var names = new []
            {
                "PosX", "NegX", "PosY", "NegY", "PosZ", "NegZ"
            };

            var skyMaterial = new Material { Shader = "SourceUtils.Shaders.Sky" };

            skyMaterial.SetBoolean( "cullFace", false );

            var hdrCompressed = false;
            var aspect = 1f;

            for ( var face = 0; face < 6; ++face )
            {
                var matPath = $"materials/skybox/{skyName}{postfixes[face]}.vmt";
                var mat = Get( bsp, matPath );

                var texProp = mat.Properties.FirstOrDefault( x => x.Name == "hdrcompressedtexture")
                    ?? mat.Properties.First(x => x.Name == "basetexture");

                if ( face == 0 )
                {
                    hdrCompressed = texProp.Name == "hdrcompressedtexture";

                    var tex = Texture.Get( bsp, TextureController.GetTexturePath( (Url) texProp.Value ) );
                    aspect = (float) tex.Width / tex.Height;
                }

                skyMaterial.SetTextureUrl($"face{names[face]}", (Url)texProp.Value);
            }

            if ( hdrCompressed )
            {
                skyMaterial.SetBoolean( "hdrCompressed", true );
            }

            if ( aspect != 1f )
            {
                skyMaterial.SetNumber( "aspect", aspect );
            }

            return skyMaterial;
        }

        [JsonProperty("shader")]
        public string Shader { get; set; }

        [JsonProperty("properties")]
        public List<MaterialProperty> Properties { get; } = new List<MaterialProperty>();

        private MaterialProperty GetOrAddProperty( string name, MaterialPropertyType type )
        {
            var prop = Properties.FirstOrDefault(x => x.Name == name);
            if ( prop == null ) Properties.Add( prop = new MaterialProperty {Name = name} );
            prop.Type = type;
            return prop;
        }

        public void SetBoolean( string name, bool value )
        {
            GetOrAddProperty( name, MaterialPropertyType.Boolean ).Value = value;
        }

        public void SetNumber( string name, float value )
        {
            GetOrAddProperty( name, MaterialPropertyType.Number ).Value = value;
        }

        public void SetTextureUrl( string name, Url textureUrl )
        {
            GetOrAddProperty( name, MaterialPropertyType.TextureUrl ).Value = textureUrl;
        }

        public void SetTextureInfo(string name, Texture value)
        {
            GetOrAddProperty(name, MaterialPropertyType.TextureInfo).Value = value;
        }

        public void SetTextureIndex(string name, int value)
        {
            GetOrAddProperty(name, MaterialPropertyType.TextureIndex).Value = value;
        }

        public void SetColor( string name, MaterialColor value )
        {
            GetOrAddProperty( name, MaterialPropertyType.Color ).Value = value;
        }
    }

    [Prefix("/materials", Extension = ".vmt.json")]
    [Prefix("/maps/{map}/materials", Extension = ".vmt.json")]
    class MaterialController : ResourceController
    {
        [Get(MatchAllUrl = false)]
        public Material Get( [Url] string map )
        {
            var path = Request.Url.AbsolutePath.Substring( Request.Url.AbsolutePath.IndexOf( "/materials" ) + 1 );

            path = HttpUtility.UrlDecode( path.Substring( 0, path.Length - ".json".Length ) );

            var bsp = map == null ? null : Program.GetMap( map );

            return Material.Get( bsp, path );
        }
    }
}
