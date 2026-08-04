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
        AddGroup(GetNextDefaultName());
    }

    private void ImageList_ImagesUpdated(object? sender, ImageListEventArgs e)
    {
        bool changed = false;
        var activeGroup = Groups.LastOrDefault() ?? AddGroup(GetNextDefaultName());

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
        var group = new DocumentGroup(string.IsNullOrWhiteSpace(indexField) ? GetNextDefaultName() : indexField.Trim());
        Groups.Add(group);
        GroupsChanged?.Invoke(this, EventArgs.Empty);
        return group;
    }

    public bool TryRenameGroup(DocumentGroup group, string? name, out string? errorMessage)
    {
        errorMessage = null;
        if (!Groups.Contains(group))
        {
            errorMessage = "This document no longer exists.";
            return false;
        }

        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Enter a document name.";
            return false;
        }
        if (Groups.Any(g => g != group && g.IndexField.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = $"A document named \"{name}\" already exists. Choose a different name.";
            return false;
        }

        group.IndexField = name;
        GroupsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void MoveImagesToGroup(IEnumerable<UiImage> images, DocumentGroup targetGroup)
    {
        if (!Groups.Contains(targetGroup))
        {
            return;
        }

        foreach (var image in images)
        {
            image.DocumentGroupId = targetGroup.Id;
            image.InvalidateThumbnail();
        }
        RemoveEmptyGroups();
        GroupsChanged?.Invoke(this, EventArgs.Empty);
    }

    public DocumentGroup? SplitAtImage(UiImage splitImage)
    {
        var images = _imageList.Images.ToList();
        int index = images.IndexOf(splitImage);
        if (index == -1) return null;

        var originalGroupId = splitImage.DocumentGroupId;
        var originalGroup = Groups.FirstOrDefault(g => g.Id == originalGroupId);
        if (originalGroup == null) return null;

        var newGroup = new DocumentGroup(GetNextDefaultName());
        
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
        if (!Groups.Contains(groupToDelete))
        {
            return;
        }

        var imagesToRemove = _imageList.Images.Where(i => i.DocumentGroupId == groupToDelete.Id).ToList();
        // Remove the group before mutating the image list so ImagesUpdated cannot
        // independently clean it up while this deletion is still in progress.
        Groups.Remove(groupToDelete);
        if (imagesToRemove.Any())
        {
            _imageList.Mutate(new ImageListMutation.DeleteSelected(), ListSelection.From(imagesToRemove));
        }

        // Restore a single empty document only when this deletion left no groups.
        // If an image-list update occurred, its normal group cleanup has already run.
        if (!Groups.Any())
        {
            AddGroup(GetNextDefaultName());
        }
        // Notify after the image mutation finishes so the tree refreshes only once,
        // with the final group list.
        GroupsChanged?.Invoke(this, EventArgs.Empty);
    }

    private string GetNextDefaultName()
    {
        int number = 1;
        while (Groups.Any(group => group.IndexField.Equals($"Document {number}", StringComparison.OrdinalIgnoreCase)))
        {
            number++;
        }
        return $"Document {number}";
    }

    private void RemoveEmptyGroups()
    {
        var emptyGroups = Groups
            .Where(group => !_imageList.Images.Any(image => image.DocumentGroupId == group.Id))
            .ToList();
        foreach (var group in emptyGroups)
        {
            Groups.Remove(group);
        }
        if (!Groups.Any())
        {
            AddGroup(GetNextDefaultName());
        }
    }
}
