using TTWWorker.Models;

namespace TTWWorker.Services;

public class KeyboardProfileService(JsonStorage storage)
{
    public IReadOnlyList<KeyboardProfile> GetAll()
    {
        var items = storage.LoadKeyboardProfiles();
        if (items.Count > 0) return items;

        var def = CreateDefault();
        storage.SaveKeyboardProfiles([def]);
        return [def];
    }

    public KeyboardProfile CreateOrUpdate(KeyboardProfile profile)
    {
        var items = storage.LoadKeyboardProfiles();
        var index = items.FindIndex(x => x.Id == profile.Id);
        if (index >= 0)
        {
            items[index] = profile;
        }
        else
        {
            items.Add(profile);
        }

        storage.SaveKeyboardProfiles(items.OrderBy(x => x.Name).ToList());
        return profile;
    }

    public KeyboardProfile? GetById(Guid id) => GetAll().FirstOrDefault(x => x.Id == id);

    public KeyboardProfile CreateDefault()
    {
        return new KeyboardProfile(
            Guid.NewGuid(),
            "default",
            7,
            20,
            30,
            10,
            60,
            ClickTimingMode.AfterDown,
            [new PixelClickPoint(960, 540, 4), new PixelClickPoint(960, 540, 4)]);
    }
}
