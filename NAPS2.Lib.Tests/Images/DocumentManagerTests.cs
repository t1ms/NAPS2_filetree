using NAPS2.Sdk.Tests;
using NSubstitute;
using Xunit;

namespace NAPS2.Lib.Tests.Images;

public class DocumentManagerTests : ContextualTests
{
    [Fact]
    public void DeleteGroup_OnlyRemovesImagesFromTheSelectedMiddleGroup()
    {
        var imageList = new UiImageList();
        var documentManager = new DocumentManager(imageList);
        var firstGroup = documentManager.Groups.Single();
        var secondGroup = documentManager.AddGroup("Document 2");
        var thirdGroup = documentManager.AddGroup("Document 3");
        var fourthGroup = documentManager.AddGroup("Document 4");
        var image1 = CreateImage(firstGroup);
        var image2 = CreateImage(secondGroup);
        var image3 = CreateImage(thirdGroup);
        var image4 = CreateImage(fourthGroup);
        AddImages(imageList, image1, image2, image3, image4);

        documentManager.DeleteGroup(secondGroup);

        Assert.Equal(new[] { image1, image3, image4 }, imageList.Images.ToArray());
        Assert.Equal(new[] { firstGroup, thirdGroup, fourthGroup }, documentManager.Groups);
        Assert.All(imageList.Images, image => Assert.NotEqual(secondGroup.Id, image.DocumentGroupId));
    }

    [Fact]
    public void DeleteGroup_RepeatedDeletionOnlyRemovesExplicitlySelectedGroups()
    {
        var imageList = new UiImageList();
        var documentManager = new DocumentManager(imageList);
        var firstGroup = documentManager.Groups.Single();
        var secondGroup = documentManager.AddGroup("Document 2");
        var thirdGroup = documentManager.AddGroup("Document 3");
        var fourthGroup = documentManager.AddGroup("Document 4");
        var image1 = CreateImage(firstGroup);
        var image2 = CreateImage(secondGroup);
        var image3 = CreateImage(thirdGroup);
        var image4 = CreateImage(fourthGroup);
        AddImages(imageList, image1, image2, image3, image4);

        documentManager.DeleteGroup(secondGroup);
        documentManager.DeleteGroup(thirdGroup);

        Assert.Equal(new[] { image1, image4 }, imageList.Images.ToArray());
        Assert.Equal(new[] { firstGroup, fourthGroup }, documentManager.Groups);
    }

    [Fact]
    public void DeleteGroup_EmptyGroupDoesNotAffectOtherDocuments()
    {
        var imageList = new UiImageList();
        var documentManager = new DocumentManager(imageList);
        var firstGroup = documentManager.Groups.Single();
        var emptyGroup = documentManager.AddGroup("Document 2");
        var thirdGroup = documentManager.AddGroup("Document 3");
        var image1 = CreateImage(firstGroup);
        var image3 = CreateImage(thirdGroup);
        AddImages(imageList, image1, image3);

        documentManager.DeleteGroup(emptyGroup);

        Assert.Equal(new[] { image1, image3 }, imageList.Images.ToArray());
        Assert.Equal(new[] { firstGroup, thirdGroup }, documentManager.Groups);
    }

    [Fact]
    public void DeleteGroup_StaleGroupDoesNothing()
    {
        var imageList = new UiImageList();
        var documentManager = new DocumentManager(imageList);
        var group = documentManager.Groups.Single();
        var image = CreateImage(group);
        AddImages(imageList, image);

        documentManager.DeleteGroup(group);
        documentManager.DeleteGroup(group);

        Assert.Empty(imageList.Images);
        Assert.Single(documentManager.Groups);
    }

    private UiImage CreateImage(DocumentGroup group)
    {
        return new UiImage(ScanningContext.CreateProcessedImage(Substitute.For<IImageStorage>()))
        {
            DocumentGroupId = group.Id
        };
    }

    private static void AddImages(UiImageList imageList, params UiImage[] images)
    {
        imageList.Mutate(new ListMutation<UiImage>.Append(images));
    }
}