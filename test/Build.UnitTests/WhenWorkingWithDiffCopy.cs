using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

using Moq;

using Psns.Common.Test.BehaviorDrivenDevelopment;

namespace Build.UnitTests
{
    public class WhenWorkingWithDiffCopy : BehaviorDrivenDevelopmentCaseTemplate
    {
        protected const string Source = "Source";
        protected const string Dest = "Destination";

        protected DiffCopy DiffCopy;
        protected bool Result;
        protected Mock<IBuildEngine> MockBuildEngine;

        public override void Arrange()
        {
            base.Arrange();

            MockBuildEngine = new Mock<IBuildEngine>();

            DiffCopy = new DiffCopy();
            DiffCopy.BuildEngine = MockBuildEngine.Object;
        }

        public override void Act()
        {
            base.Act();

            Result = DiffCopy.Execute();
        }

        public override void CleanUp()
        {
            new[] { Source, Dest }.ToList().ForEach(path =>
                {
                    if(Directory.Exists(path))
                        Directory.Delete(path, true);
                });
        }
    }

    public class TaskItemImpl : ITaskItem
    {
        public IDictionary CloneCustomMetadata()
        {
            throw new NotImplementedException();
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            throw new NotImplementedException();
        }

        public string GetMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public string ItemSpec { get; set; }

        public int MetadataCount
        {
            get { throw new NotImplementedException(); }
        }

        public ICollection MetadataNames
        {
            get { throw new NotImplementedException(); }
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            throw new NotImplementedException();
        }
    }

    [TestClass]
    public class AndSourceDoesntExist : WhenWorkingWithDiffCopy
    {
        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenNoFilesShouldBeCopiedAndFalseReturned()
        {
            Assert.AreEqual<int>(0, DiffCopy.FilesCopied.Length);
            Assert.IsFalse(Result);

            MockBuildEngine.Verify(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()), Times.Once());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndDestinationDoesntExist : WhenWorkingWithDiffCopy
    {
        public override void Arrange()
        {
            base.Arrange();

            Directory.CreateDirectory(Path.Combine(Source, "Sub1"));
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenNoFilesShouldBeCopiedAndFalseReturned()
        {
            Assert.AreEqual<int>(0, DiffCopy.FilesCopied.Length);
            Assert.IsFalse(Result);

            MockBuildEngine.Verify(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()), Times.Once());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndFilesIsNull : WhenWorkingWithDiffCopy
    {
        string _subPath;

        public override void Arrange()
        {
            base.Arrange();

            _subPath = Path.Combine("Sub1", "Sub2");
            string sourcePath = Path.Combine(Source, _subPath);

            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(Dest);
            Directory.CreateDirectory(Path.Combine(Source, "Sub3"));

            using(var writer = File.CreateText(Path.Combine(sourcePath, "text1.txt")))
            {
                writer.Write("here is some text");
            }

            using(var writer = File.CreateText(Path.Combine(Source, "Sub3\\text3.txt")))
            {
                writer.Write("here is some text");
            }
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            base.Act();
        }

        [TestMethod]
        public void ThenOneFileShouldBeCopied()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(2, DiffCopy.FilesCopied.Length);

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(5));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndCopyingDirectoriesWithSameFiles : WhenWorkingWithDiffCopy
    {
        string _subPath;

        public override void Arrange()
        {
            base.Arrange();

            _subPath = Path.Combine("Sub1", "Sub2");
            string sourcePath = Path.Combine(Source, _subPath);
            string destPath = Path.Combine(Dest, _subPath);

            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(destPath);

            new[] { sourcePath, destPath }.ToList().ForEach(path =>
                {
                    string text1Path = Path.Combine(path, "text1.txt");
                    using(var writer = File.CreateText(text1Path))
                    {
                        writer.Write("here is some text");
                    }
                });
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenNoFilesShouldBeCopied()
        {
            Assert.AreEqual<int>(0, DiffCopy.FilesCopied.Length);
            Assert.IsTrue(Result);

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(1));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndCopyingDirectoriesWithDifferingFilesOfSameName : WhenWorkingWithDiffCopy
    {
        string _subPath;

        public override void Arrange()
        {
            base.Arrange();

            _subPath = Path.Combine("Sub1", "Sub2");
            string sourcePath = Path.Combine(Source, _subPath);
            string destPath = Path.Combine(Dest, _subPath);

            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(destPath);

            new[] { sourcePath, destPath }.ToList().ForEach(path =>
            {
                using(var writer = File.CreateText(Path.Combine(path, "text1.txt")))
                {
                    writer.Write(string.Format("here is some text for {0}", path));
                }
            });
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenOneFilesShouldBeCopied()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(1, DiffCopy.FilesCopied.Length);

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(1));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndCopyingANewFileWithNewDirectory : WhenWorkingWithDiffCopy
    {
        string _subPath;

        public override void Arrange()
        {
            base.Arrange();

            _subPath = Path.Combine("Sub1", "Sub2");
            string sourcePath = Path.Combine(Source, _subPath);

            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(Dest);

            using(var writer = File.CreateText(Path.Combine(sourcePath, "text1.txt")))
            {
                writer.Write("here is some text");
            }
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenOneFileShouldBeCopied()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(1, DiffCopy.FilesCopied.Length);

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(3));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndCopyingAMoreComplicatedTreeWithExcludes : WhenWorkingWithDiffCopy
    {
        string _subPath;

        public override void Arrange()
        {
            base.Arrange();

            _subPath = Path.Combine("Sub1", "Sub2");
            string sourcePath = Path.Combine(Source, _subPath);
            string destPath = Path.Combine(Dest, _subPath);

            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(destPath);
            Directory.CreateDirectory(Path.Combine(Source, "Sub3"));

            using(var writer = File.CreateText(Path.Combine(Source, "text1.txt")))
            {
                writer.Write("here is some text");
            }

            using(var writer = File.CreateText(Path.Combine(Source + "\\Sub1", "text2.txt")))
            {
                writer.Write("here is some text");
            }

            using(var writer = File.CreateText(Path.Combine(Source + "\\Sub1", "textToIgnore.txt")))
            {
                writer.Write("here is some text");
            }

            using(var writer = File.CreateText(Path.Combine(Source + "\\Sub3", "textToIgnore.txt")))
            {
                writer.Write("here is some text");
            }

            new[] { sourcePath, destPath }.ToList().ForEach(path =>
            {
                string text1Path = Path.Combine(path, "text1.txt");
                using(var writer = File.CreateText(text1Path))
                {
                    writer.Write("here is some text");
                }
            });

            using(var writer = File.CreateText(Path.Combine(sourcePath, "text2.txt")))
            {
                writer.Write("here is some text for {0}", sourcePath);
            }
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" },
                new TaskItemImpl { ItemSpec = "Sub3" },
                new TaskItemImpl { ItemSpec = "text1.txt" },
            };

            DiffCopy.Excluded = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "sub2" },
                new TaskItemImpl { ItemSpec = "Sub1\\textToIgnore.txt" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenTwoFilesShouldBeCopied()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(3, DiffCopy.FilesCopied.Length);

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(6));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndDestinationHasExtraFiles : WhenWorkingWithDiffCopy
    {
        string[] _extraFilePaths;

        public override void Arrange()
        {
            base.Arrange();

            _extraFilePaths = new string[]
            {
                Path.Combine(Dest, "RootFile1.txt"),
                Path.Combine(Dest, "DestSub1\\File1.txt")
            };

            Directory.CreateDirectory(Source);
            Directory.CreateDirectory(Path.Combine(Dest, "DestSub1"));

            using(var writer = File.CreateText(Path.Combine(Source, "testfile.txt")))
            {
                writer.Write("test file");
            }

            using(var writer = File.CreateText(_extraFilePaths[0]))
            {
                writer.Write("test file");
            }

            using(var writer = File.CreateText(_extraFilePaths[1]))
            {
                writer.Write("test file");
            }
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            base.Act();
        }

        [TestMethod]
        public void ThenExtraFilesShouldBeDeleted()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(1, DiffCopy.FilesCopied.Length);

            _extraFilePaths.ToList().ForEach(path =>
            {
                Assert.IsFalse(File.Exists(path));
            });

            Assert.IsTrue(File.Exists(Path.Combine(Dest, "testfile.txt")));

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(3));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndDestinationHasExtraFilesThatAreIgnored : WhenWorkingWithDiffCopy
    {
        string[] _extraFilePaths;

        public override void Arrange()
        {
            base.Arrange();

            _extraFilePaths = new string[]
            {
                Path.Combine(Dest, "RootFile1.txt"),
                Path.Combine(Dest, "DestSub1\\File1.txt")
            };

            Directory.CreateDirectory(Source);
            Directory.CreateDirectory(Path.Combine(Dest, "DestSub1"));

            using(var writer = File.CreateText(Path.Combine(Source, "testfile.txt")))
            {
                writer.Write("test file");
            }

            using(var writer = File.CreateText(_extraFilePaths[0]))
            {
                writer.Write("test file");
            }

            using(var writer = File.CreateText(_extraFilePaths[1]))
            {
                writer.Write("test file");
            }
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Excluded = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = ".txt" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenExtraFilesThatAreIgnoredShouldNotBeDeleted()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(0, DiffCopy.FilesCopied.Length);

            Assert.IsTrue(File.Exists(Path.Combine(Dest, "RootFile1.txt")));

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(3));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanUp();
        }
    }

    [TestClass]
    public class AndCopyingDirectoriesWithDifferingFilesWhereDestIsReadOnly : WhenWorkingWithDiffCopy
    {
        string _subPath;

        public override void Arrange()
        {
            base.Arrange();

            _subPath = Path.Combine("Sub1", "Sub2");
            string sourcePath = Path.Combine(Source, _subPath);
            string destPath = Path.Combine(Dest, _subPath);

            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(destPath);

            new[] { sourcePath, destPath }.ToList().ForEach(path =>
            {
                using(var writer = File.CreateText(Path.Combine(path, "text1.txt")))
                {
                    writer.Write(string.Format("here is some text for {0}", path));
                }
            });

            using(var writer = File.CreateText(Path.Combine(destPath, "tobedeleted.txt")))
            {
                writer.Write("Here is some text");
            }

            Directory.CreateDirectory(Path.Combine(destPath, "ToBeDeleted"));

            new[] { "text1.txt", "ToBeDeleted", "tobedeleted.txt" }.ToList().ForEach(fileName =>
            {
                File.SetAttributes(Path.Combine(destPath, fileName), FileAttributes.ReadOnly);
            });
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenOneFilesShouldBeCopiedAndReadOnlyShouldBeReApplied()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(1, DiffCopy.FilesCopied.Length);

            Assert.IsTrue((File.GetAttributes(Path.Combine(Dest, _subPath, "text1.txt")) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            Assert.IsFalse(File.Exists(Path.Combine(Dest, _subPath, "tobedeleted.txt")));
            Assert.IsFalse(Directory.Exists(Path.Combine(Dest, _subPath, "ToBeDeleted")));

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(1));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            var attributes = File.GetAttributes(Path.Combine(Dest, _subPath, "text1.txt"));
            File.SetAttributes(Path.Combine(Dest, _subPath, "text1.txt"), attributes & ~FileAttributes.ReadOnly);
            CleanUp();
        }
    }

    [TestClass]
    public class AndCopyingDirectoriesWithDifferingFilesWhereDestDirectoryContainsReadOnlyFiles : WhenWorkingWithDiffCopy
    {
        string _subPath;

        public override void Arrange()
        {
            base.Arrange();

            _subPath = Path.Combine("Sub1", "Sub2");
            string sourcePath = Path.Combine(Source, _subPath);
            string destPath = Path.Combine(Dest, _subPath);

            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(destPath);

            new[] { sourcePath, destPath }.ToList().ForEach(path =>
            {
                using(var writer = File.CreateText(Path.Combine(path, "text1.txt")))
                {
                    writer.Write(string.Format("here is some text for {0}", path));
                }
            });

            using(var writer = File.CreateText(Path.Combine(destPath, "tobedeleted.txt")))
            {
                writer.Write("Here is some text");
            }

            Directory.CreateDirectory(Path.Combine(destPath, "ToBeDeleted"));

            new[] { "text1.txt", "ToBeDeleted", "tobedeleted.txt" }.ToList().ForEach(fileName =>
            {
                File.SetAttributes(Path.Combine(destPath, fileName), FileAttributes.ReadOnly);
            });

            var readOnlyFilePathToBeDeleted = Path.Combine(Path.Combine(destPath, "ToBeDeleted"), "readOnlyToBeDeleted.txt");
            using(var writer = File.CreateText(readOnlyFilePathToBeDeleted))
            {
                writer.Write("here is some text for");
            }

            File.SetAttributes(readOnlyFilePathToBeDeleted, FileAttributes.ReadOnly);
        }

        public override void Act()
        {
            DiffCopy.Source = new TaskItemImpl { ItemSpec = Source };
            DiffCopy.Destination = new TaskItemImpl { ItemSpec = Dest };

            DiffCopy.Files = new TaskItemImpl[]
            {
                new TaskItemImpl { ItemSpec = "Sub1" }
            };

            base.Act();
        }

        [TestMethod]
        public void ThenOneFilesShouldBeCopiedAndReadOnlyShouldBeReApplied()
        {
            Assert.IsTrue(Result);
            Assert.AreEqual<int>(1, DiffCopy.FilesCopied.Length);

            Assert.IsTrue((File.GetAttributes(Path.Combine(Dest, _subPath, "text1.txt")) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            Assert.IsFalse(File.Exists(Path.Combine(Dest, _subPath, "tobedeleted.txt")));
            Assert.IsFalse(Directory.Exists(Path.Combine(Dest, _subPath, "ToBeDeleted")));

            MockBuildEngine.Verify(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.AtLeast(1));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            var attributes = File.GetAttributes(Path.Combine(Dest, _subPath, "text1.txt"));
            File.SetAttributes(Path.Combine(Dest, _subPath, "text1.txt"), attributes & ~FileAttributes.ReadOnly);
            CleanUp();
        }
    }
}