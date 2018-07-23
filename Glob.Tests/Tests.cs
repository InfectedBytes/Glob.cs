﻿using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ganss.IO;
using System.Reflection;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Abstractions;

#pragma warning disable 1591
namespace Ganss.IO.Tests
{
    public class Tests
    {
        public MockFileSystem FileSystem { get; set; }
        const string TestDir = "c:";

        public Tests()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                [$@"{TestDir}\d"] = new MockFileData(""),
                [$@"{TestDir}\dir1\abc"] = new MockFileData(""),
                [$@"{TestDir}\dir2\dir1\123"] = new MockFileData(""),
                [$@"{TestDir}\dir2\dir1\456"] = new MockFileData(""),
                [$@"{TestDir}\dir2\dir2\file1"] = new MockFileData(""),
                [$@"{TestDir}\dir2\dir2\file2"] = new MockFileData(""),
                [$@"{TestDir}\dir2\dir2\file3"] = new MockFileData(""),
                [$@"{TestDir}\dir2\dir2\xyz"] = new MockFileData(""),
                [$@"{TestDir}\dir2\file1"] = new MockFileData(""),
                [$@"{TestDir}\dir2\file2"] = new MockFileData(""),
                [$@"{TestDir}\dir2\file3"] = new MockFileData(""),
                [$@"{TestDir}\dir3\file1"] = new MockFileData(""),
                [$@"{TestDir}\dir3\xyz"] = new MockFileData(""),
                [$@"{TestDir}\file1"] = new MockFileData(""),
                [$@"{TestDir}\[dir"] = new MockFileData(""),
                [$@"{TestDir}\[dir]"] = new MockFileData(""),
                [$@"{TestDir}\{{dir"] = new MockFileData(""),
                [$@"{TestDir}\{{dir1"] = new MockFileData(""),
                [$@"{TestDir}\{{dir1}}"] = new MockFileData(""),
            });

            fileSystem.Directory.SetCurrentDirectory($@"{TestDir}\dir2\dir1");

            FileSystem = fileSystem;
        }

        IEnumerable<string> ExpandNames(string pattern, bool ignoreCase = true, bool dirOnly = false)
        {
            return new Glob(TestDir + pattern, FileSystem) { IgnoreCase = ignoreCase, DirectoriesOnly = dirOnly }.ExpandNames();
        }

        void AssertEqual(IEnumerable<string> actual, params string[] expected)
        {
            var exp = expected.Select(f => TestDir + f).ToList();
            var act = actual.ToList();
            Assert.Equal(exp, act);
        }

        [Fact]
        public void CanExpandSimpleCases()
        {
            AssertEqual(ExpandNames(@"\file1"), @"\file1");
            AssertEqual(ExpandNames(@"\dir3\file*"), @"\dir3\file1");
            AssertEqual(ExpandNames(@"\dir2\*\*1*"), @"\dir2\dir1\123", @"\dir2\dir2\file1");
            AssertEqual(ExpandNames(@"\**\file1"), @"\file1", @"\dir2\file1", @"\dir2\dir2\file1", @"\dir3\file1");
            AssertEqual(ExpandNames(@"\**\*xxx"));
            AssertEqual(ExpandNames(@"\dir2\file[13]"), @"\dir2\file1", @"\dir2\file3");
            AssertEqual(ExpandNames(@"\dir3\???"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\dir3\[^f]*"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\dir3\[g-z]*"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\[dir]"), @"\d");
            AssertEqual(ExpandNames(@"\**\d"), @"\d");
        }

        [Fact]
        public void CanExpandGroups()
        {
            AssertEqual(ExpandNames(@"\dir{1,3}\???"), @"\dir1\abc", @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\{dir1,dir3}\???"), @"\dir1\abc", @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\dir2\{file*,dir1\1?3}"), @"\dir2\file1", @"\dir2\file2", @"\dir2\file3", @"\dir2\dir1\123");
            AssertEqual(ExpandNames(@"\{dir3,dir2\{dir1,dir2}}\file1"), @"\dir3\file1", @"\dir2\dir2\file1");
            AssertEqual(ExpandNames(@"\dir{3,2\dir{1,2}}\[fgh]*1"), @"\dir3\file1", @"\dir2\dir2\file1");
            AssertEqual(ExpandNames(@"\{d,e}ir1\???"), @"\dir1\abc");
        }

        [Fact]
        public void CanExpandStrangeCases()
        {
            AssertEqual(ExpandNames(@"\*******\[aaaaaaaaaaaa]?c"), @"\dir1\abc");
            AssertEqual(ExpandNames(@"\******\xyz"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\**\**\**\**\4?[0-9]*"), @"\dir2\dir1\456");
            AssertEqual(ExpandNames(@"\**\*******xyz*******"), @"\dir2\dir2\xyz", @"\dir3\xyz");
        }

        [Fact]
        public void CanExpandNonGlobParts()
        {
            AssertEqual(ExpandNames(@"\[dir"), @"\[dir");
            AssertEqual(ExpandNames(@"\{dir"), @"\{dir");
            AssertEqual(ExpandNames(@"\{dir}"));
            AssertEqual(ExpandNames(@"\>"));
        }

        [Fact]
        public void CanMatchCase()
        {
            AssertEqual(ExpandNames(@"\Dir2\file[13]", ignoreCase: false));
            AssertEqual(ExpandNames(@"\dir2\file[13]"), @"\dir2\file1", @"\dir2\file3");
        }

        [Fact]
        public void CanMatchDirOnly()
        {
            AssertEqual(ExpandNames(@"\**\dir*", ignoreCase: true, dirOnly: true), @"\dir1", @"\dir2", @"\dir3",
                @"\dir2\dir1", @"\dir2\dir2");
        }

        [Fact]
        public void CanMatchRelativePaths()
        {
            AssertEqual(Glob.ExpandNames(@"..\..\dir3\file*", ignoreCase: true, dirOnly: false, fileSystem: FileSystem), @"\dir3\file1");
            AssertEqual(Glob.ExpandNames(@".\..\..\.\.\dir3\file*", ignoreCase: true, dirOnly: false, fileSystem: FileSystem), @"\dir3\file1");
            var cwd = FileSystem.Directory.GetCurrentDirectory();
            var dir = FileSystem.Directory.GetParent(cwd).Parent.FullName;
            dir = dir.Substring(2).TrimEnd('\\'); // C:\xyz -> \xyz
            AssertEqual(Glob.ExpandNames(dir + @"\dir3\file*", ignoreCase: true, dirOnly: false, fileSystem: FileSystem), @"\dir3\file1");
        }

        [Fact]
        public void CanCancel()
        {
            var glob = new Glob(TestDir + @"\dir1\*", FileSystem);
            glob.Cancel();
            var fs = glob.Expand().ToList();
            Assert.Empty(fs);
        }

        [Fact]
        public void CanLog()
        {
            var log = "";
            var glob = new Glob(@"ü:\\x", new TestFileSystem()) { IgnoreCase = true, ErrorLog = s => log += s };
            var fs = glob.ExpandNames().ToList();
            Assert.False(string.IsNullOrEmpty(log));
        }

        [Fact]
        public void CanUseStaticMethods()
        {
            var fs = Glob.Expand(TestDir + @"\dir1\abc", ignoreCase: true, dirOnly: false, fileSystem: FileSystem).Select(f => f.FullName).ToList();
            AssertEqual(fs, @"\dir1\abc");
        }

        [Fact]
        public void CanUseUncachedRegex()
        {
            var fs = new Glob(TestDir + @"\dir1\*", FileSystem) { CacheRegexes = false }.ExpandNames().ToList();
            AssertEqual(fs, @"\dir1\abc");
        }

        [Fact]
        public void DetectsInvalidPaths()
        {
            ExpandNames(@"\>\xyz", ignoreCase: false).ToList();
            var n = new Glob(@"ü:\x", FileSystem) { IgnoreCase = false }.ExpandNames().ToList();
            Assert.Empty(n);
        }

        [Fact]
        public void CanMatchRelativeChildren()
        {
            AssertEqual(ExpandNames(@"\dir1\.", ignoreCase: false, dirOnly: true), @"\dir1");
            AssertEqual(ExpandNames(@"\dir2\dir1\..", ignoreCase: false, dirOnly: true), @"\dir2");
        }

        [Fact]
        public void ReturnsStringAndHash()
        {
            var glob = new Glob("abc", FileSystem);
            Assert.Equal("abc", glob.ToString());
            Assert.Equal("abc".GetHashCode(), glob.GetHashCode());
        }

        [Fact]
        public void CanCompareInstances()
        {
            var glob = new Glob("abc", FileSystem);
            Assert.False(glob.Equals(4711));
            Assert.True(glob.Equals(new Glob("abc")));
        }

        [Fact]
        public void CanThrow()
        {
            var fs = new TestFileSystem
            {
                FileInfo = new TestFileInfoFactory() { FromFileNameFunc = n => throw new ArgumentException("", "1") }
            };

            var g = new Glob(TestDir + @"\>", fs) { ThrowOnError = true };
            Assert.Throws<ArgumentException>("1", () => g.ExpandNames().ToList());

            fs.Path = new TestPath(FileSystem) { GetDirectoryNameFunc = n => throw new ArgumentException("", "2") };

            g = new Glob("*", fs);
            Assert.Empty(g.ExpandNames());
            g.ThrowOnError = true;
            Assert.Throws<ArgumentException>("2", () => g.ExpandNames().ToList());

            fs.Path = new TestPath(FileSystem) { GetDirectoryNameFunc = n => null };
            fs.DirectoryInfo = new TestDirectoryInfoFactory { FromDirectoryNameFunc = n => throw new ArgumentException("", "3") };

            g.ThrowOnError = false;
            Assert.Empty(g.ExpandNames());
            g.ThrowOnError = true;
            Assert.Throws<ArgumentException>("3", () => g.ExpandNames().ToList());

            fs.Path = new TestPath(FileSystem) { GetDirectoryNameFunc = n => "" };
            fs.DirectoryInfo = new TestDirectoryInfoFactory { FromDirectoryNameFunc = n => null };
            fs.Directory = new TestDirectory(FileSystem, null, "") { GetCurrentDirectoryFunc = () => throw new ArgumentException("", "4") };

            g.ThrowOnError = false;
            Assert.Empty(g.ExpandNames());
            g.ThrowOnError = true;
            Assert.Throws<ArgumentException>("4", () => g.ExpandNames().ToList());

            fs.Directory = new TestDirectory(FileSystem, null, "") { GetCurrentDirectoryFunc = () => "5" };
            var d = new TestDirectoryInfo(FileSystem, TestDir) { GetFileSystemInfosFunc = () => throw new ArgumentException("", "5") };
            fs.DirectoryInfo = new TestDirectoryInfoFactory { FromDirectoryNameFunc = n => d };

            g.ThrowOnError = false;
            Assert.Empty(g.ExpandNames());
            g.ThrowOnError = true;
            Assert.Throws<ArgumentException>("5", () => g.ExpandNames().ToList());
        }
    }
}
#pragma warning restore 1591