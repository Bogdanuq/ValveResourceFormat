using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ValveResourceFormat;
using ValveResourceFormat.IO;

namespace Tests
{
    public class PartialReadTest
    {
        private static string TestFile(string name) => Path.Combine(TestContext.TestDirectory!, "Files", name);

        private static Resource ReadOnDemand(string name)
        {
            var resource = new Resource { ReadBlocksOnDemand = true };
            resource.Read(TestFile(name));
            return resource;
        }

        [Test]
        public async Task OnDemandReadDefersAndMaterializesBlocks()
        {
            using var fullResource = new Resource();
            fullResource.Read(TestFile("alchemist.vmdl_c"));

            using var partialResource = ReadOnDemand("alchemist.vmdl_c");

            var deferredData = partialResource.Blocks.First(block => block.Type == BlockType.DATA);
            await Assert.That(deferredData.IsRead).IsFalse();

            var dataBlock = partialResource.DataBlock;

            using (Assert.Multiple())
            {
                await Assert.That(dataBlock).IsNotNull();
                await Assert.That(deferredData.IsRead).IsTrue();
            }

            await Assert.That(dataBlock!.ToString()).IsEqualTo(fullResource.DataBlock!.ToString());

            var fullReferences = fullResource.ExternalReferences?.ResourceRefInfoList.Select(static reference => reference.Name).ToList();
            var partialReferences = partialResource.ExternalReferences?.ResourceRefInfoList.Select(static reference => reference.Name).ToList();
            await Assert.That(partialReferences).IsEquivalentTo(fullReferences!);
        }

        [Test]
        public async Task OnDemandVDataIsSpecialized()
        {
            using var fullResource = new Resource();
            fullResource.Read(TestFile("abilities_kv3_v5_zstd.vdata_c"));

            using var partialResource = ReadOnDemand("abilities_kv3_v5_zstd.vdata_c");

            using (Assert.Multiple())
            {
                await Assert.That(partialResource.Blocks.First(static block => block.Type == BlockType.DATA).IsRead).IsTrue();
                await Assert.That(partialResource.DataBlock!.GetType()).IsEqualTo(fullResource.DataBlock!.GetType());
            }
        }

        [Test]
        [Arguments("alchemist.vmdl_c", BlockType.NTRO)]
        [Arguments("box_creature_model.vmdl_c", BlockType.CTRL)]
        public async Task DependencyBlocksParseOnFirstUse(string file, BlockType dependency)
        {
            using var fullResource = new Resource();
            fullResource.Read(TestFile(file));

            using var resource = ReadOnDemand(file);
            var dependencyBlock = resource.Blocks.First(block => block.Type == dependency);

            using (Assert.Multiple())
            {
                await Assert.That(resource.ResourceType).IsEqualTo(ResourceType.Model);
                await Assert.That(dependencyBlock.IsRead).IsFalse();
            }

            await Assert.That(resource.DataBlock!.ToString()).IsEqualTo(fullResource.DataBlock!.ToString());
            await Assert.That(resource.GetBlockByType(dependency)!.ToString()).IsEqualTo(fullResource.GetBlockByType(dependency)!.ToString());
        }

        [Test]
        public async Task OnDemandEditInfoParsesOnFirstUse()
        {
            using var fullResource = new Resource();
            fullResource.Read(TestFile("reflectivity_90b.vmat_c"));

            using var partialResource = ReadOnDemand("reflectivity_90b.vmat_c");
            var editInfoBlock = partialResource.Blocks.First(static block => block.Type is BlockType.REDI or BlockType.RED2);

            await Assert.That(editInfoBlock.IsRead).IsFalse();

            var editInfo = partialResource.EditInfo;

            using (Assert.Multiple())
            {
                await Assert.That(editInfoBlock.IsRead).IsTrue();
                await Assert.That(editInfo!.ToString()).IsEqualTo(fullResource.EditInfo!.ToString());
            }
        }

        [Test]
        public async Task OnDemandEditInfoDeterminesUnknownType()
        {
            using var fullStream = File.OpenRead(TestFile("reflectivity_90b.vmat_c"));
            using var fullResource = new Resource();
            fullResource.Read(fullStream, leaveOpen: true);

            using var partialStream = File.OpenRead(TestFile("reflectivity_90b.vmat_c"));
            using var partialResource = new Resource { ReadBlocksOnDemand = true };
            partialResource.Read(partialStream, leaveOpen: true);

            using (Assert.Multiple())
            {
                await Assert.That(fullResource.ResourceType).IsEqualTo(ResourceType.Material);
                await Assert.That(partialResource.ResourceType).IsEqualTo(ResourceType.Material);
                await Assert.That(partialResource.DataBlock!.ToString()).IsEqualTo(fullResource.DataBlock!.ToString());
            }
        }

        [Test]
        public async Task ConcurrentMaterializationMatchesFullRead()
        {
            using var fullResource = new Resource();
            fullResource.Read(TestFile("export_test.vmdl_c"));
            var expected = fullResource.Blocks.Select(static block => block.ToString()).ToList();

            using var partialResource = ReadOnDemand("export_test.vmdl_c");

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                for (var i = partialResource.Blocks.Count - 1; i >= 0; i--)
                {
                    partialResource.GetBlockByIndex(i);
                }
            })));

            await Assert.That(partialResource.Blocks.Select(static block => block.ToString()).ToList()).IsEquivalentTo(expected);
        }

        [Test]
        public async Task MaterializingAfterDisposeThrows()
        {
            var resource = ReadOnDemand("alchemist.vmdl_c");
            var data = resource.Blocks.First(static block => block.Type == BlockType.DATA);
            resource.Dispose();

            await Assert.That(data.EnsureRead).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task LoaderExtensionReadsOnDemand()
        {
            using var loader = new GameFileLoader(null, TestFile("alchemist.vmdl_c"));
            loader.AddDiskPathToSearch(Path.Combine(TestContext.TestDirectory!, "Files"));

            using var resource = loader.LoadFileCompiledOnDemand("alchemist.vmdl");

            await Assert.That(resource).IsNotNull();
            await Assert.That(resource!.Blocks.First(static block => block.Type == BlockType.DATA).IsRead).IsFalse();
        }
    }
}
