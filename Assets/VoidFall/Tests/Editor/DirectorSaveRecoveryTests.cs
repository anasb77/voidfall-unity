using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Persistence;

namespace VoidFall.Tests.Editor
{
    public sealed class DirectorSaveRecoveryTests
    {
        [Test]
        public void Explicit_reload_recovers_a_read_lock_without_writing_temporary_defaults()
        {
            var directory=Path.Combine(Path.GetTempPath(),"voidfall-director-reload-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path=Path.Combine(directory,"profile.json");var store=new SaveStore(path);
                var original=SaveStore.CreateDefault();original.parts=427;store.Save(original);
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    LogAssert.Expect(LogType.Error,new Regex("VoidFall save could not be read"));
                    store.Load();Assert.That(store.StorageUnreadable,Is.True);
                    Assert.That(store.TryReloadExisting(out _),Is.False);
                    Assert.Throws<IOException>(()=>store.Save(SaveStore.CreateDefault()));
                }
                Assert.That(store.TryReloadExisting(out var recovered),Is.True);
                Assert.That(recovered.parts,Is.EqualTo(427));Assert.That(store.StorageUnreadable,Is.False);
                recovered.parts+=12;store.Save(recovered);
                Assert.That(new SaveStore(path).Load().parts,Is.EqualTo(439));
            }
            finally { Directory.Delete(directory,true); }
        }

        [Test]
        public void Recovery_does_not_create_a_blank_profile_when_files_are_missing()
        {
            var directory=Path.Combine(Path.GetTempPath(),"voidfall-director-missing-"+Guid.NewGuid().ToString("N"));
            var store=new SaveStore(Path.Combine(directory,"profile.json"));
            Assert.That(store.TryReloadExisting(out _),Is.False);
            Assert.That(Directory.Exists(directory),Is.False);
        }
    }
}
