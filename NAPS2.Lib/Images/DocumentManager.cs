using System;
using System.Collections.Generic;
using System.Linq;

namespace NAPS2.Images;

public class DocumentManager
{
    private readonly UiImageList _imageList;

    public List<DocumentGroup> Groups { get; } = new();

    public event EventHandler? GroupsChanged;

    public DocumentManager(UiImageList imageList)
    {
        _imageList = imageList;
        _imageList.ImagesUpdated += ImageList_ImagesUpdated;
        
        // Start with a default group
        AddGroup("Document 1");
    }

    private void ImageList_ImagesUpdated(object? sender, ImageListEventArgs e)
    {
        bool changed = false;
        var activeGroup = Groups.LastOrDefault() ?? AddGroup("Document 1");

        foreach (var image in _imageList.Images)
        {
            if (image.DocumentGroupId == Guid.Empty)
            {
                image.DocumentGroupId = activeGroup.Id;
                changed = true;
            }
        }

        // Clean up empty groups if needed, or reorder, etc.
        // Let's remove any groups that have no images AND are not the only group.
        var groupsToRemove = Groups.Where(g => g != activeGroup && !_imageList.Images.Any(i => i.DocumentGroupId == g.Id)).ToList();
        foreach (var g in groupsToRemove)
        {
            Groups.Remove(g);
            changed = true;
        }

        if (changed)
        {
            GroupsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public DocumentGroup AddGroup(string indexField)
    {
        var group = new DocumentGroup(indexField);
        Groups.Add(group);
        GroupsChanged?.Invoke(this, EventArgs.Empty);
        return group;
    }

    public DocumentGroup? SplitAtImage(UiImage splitImage)
    {
        var images = _imageList.Images.ToList();
        int index = images.IndexOf(splitImage);
        if (index == -1) return null;

        var originalGroupId = splitImage.DocumentGroupId;
        var originalGroup = Groups.FirstOrDefault(g => g.Id == originalGroupId);
        if (originalGroup == null) return null;

        // Determine a new name
        string newName = "Document " + (Groups.Count + 1);
        var newGroup = new DocumentGroup(newName);
        
        // Insert it right after the original group
        int groupIndex = Groups.IndexOf(originalGroup);
        Groups.Insert(groupIndex + 1, newGroup);

        for (int i = index; i < images.Count; i++)
        {
            // Only split images that were in the original group (contiguous split)
            if (images[i].DocumentGroupId == originalGroupId)
            {
                images[i].DocumentGroupId = newGroup.Id;
            }
            else
            {
                // Once we hit another group, stop.
                break;
            }
        }

        GroupsChanged?.Invoke(this, EventArgs.Empty);
        return newGroup;
    }

    public void MergeWithPrevious(DocumentGroup groupToMerge)
    {
        int index = Groups.IndexOf(groupToMerge);
        if (index <= 0) return; // Cannot merge the first group with previous

        var targetGroup = Groups[index - 1];

        // Reassign all images in groupToMerge to targetGroup
        foreach (var image in _imageList.Images.Where(i => i.DocumentGroupId == groupToMerge.Id))
        {
            image.DocumentGroupId = targetGroup.Id;
        }

        Groups.Remove(groupToMerge);
        GroupsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteGroup(DocumentGroup groupToDelete)
    {
        var imagesToRemove = _imageList.Images.Where(i => i.DocumentGroupId == groupToDelete.Id).ToList();
        if (imagesToRemove.Any())
        {
            _imageList.Mutate(new ImageListMutation.DeleteSelected(), ListSelection.From(imagesToRemove));
        }
        
        if (Groups.Contains(groupToDelete))
        {
            Groups.Remove(groupToDelete);
            GroupsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
