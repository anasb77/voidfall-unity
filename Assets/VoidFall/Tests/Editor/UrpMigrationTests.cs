using NUnit.Framework;
using UnityEngine;
using VoidFall.Runtime.Rendering;

namespace VoidFall.Tests.Editor
{
    public sealed class UrpMigrationTests
    {
        [Test]
        public void Explicit_material_resources_use_the_expected_urp_shaders()
        {
            Assert.That(VoidFallRenderMaterials.DefaultUnlit.shader.name, Is.EqualTo("VoidFall/DefaultUnlit"));
            Assert.That(VoidFallRenderMaterials.AdditiveSprite.shader.name, Is.EqualTo("VoidFall/AdditiveSprite"));
            Assert.That(VoidFallRenderMaterials.ScreenBlend.shader.name, Is.EqualTo("VoidFall/ScreenBlend"));
        }

        [Test]
        public void Filament_instances_are_copies_of_the_explicit_template()
        {
            var filament = VoidFallRenderMaterials.CreateFilamentInstance();
            try
            {
                Assert.That(filament.shader.name, Is.EqualTo("VoidFall/FilamentGas"));
                Assert.That(filament, Is.Not.SameAs(VoidFallRenderMaterials.FilamentTemplate));
            }
            finally
            {
                Object.DestroyImmediate(filament);
            }
        }
    }
}
