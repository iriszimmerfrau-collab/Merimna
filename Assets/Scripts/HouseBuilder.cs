using UnityEngine;

/// <summary>
/// Builds the little house on the island plus its interior room.
/// The room lives far outside the world; the door fades the screen to black (Omori style),
/// teleports the player in/out and swaps the camera bounds.
/// Inside: a bed (sleep + heal), a knife on a table, a journal, a lamp and a plant.
/// </summary>
public static class HouseBuilder
{
    const float RoomW = 22f, RoomH = 12f;

    static readonly string[] JournalPages =
    {
        "day 1\n\nthe fish in the sky are learning my name.\nthey say it wrong on purpose.",
        "THE FOXES ARE NOT FOXES.\n\ni counted them twice.\nthe second number was a color.",
        "i watered the plant with the lamp's light.\nit is thirsty for the wrong thing now.\n\nit says thank you in my handwriting.",
        "someone moves the house a little every night.\n3 inches.\ntoward the water.\n\ni measured the door. the door measured me back.",
        "recipe for a quiet afternoon:\n\n2 spoons of static\n1 spoon of grandma's voice\ndo not stir clockwise\ndo not stir",
        "the whales sing in a language made of doors.\n\ni know where every one of them leads.\ni won't tell the cat.",
        "i i i i i i i i i i i i i i i\ni i i i i i i i i i i i i i i\ni i i i i i i i i i i i i i i\n\nwho is the other i",
        "the moon is a button.\n\nDON'T.\n\n(everything is fine. the walls said so.\nthe walls are always kind.)",
        "this page is intentionally left\n\n\n\n\n\nbehind you",
    };

    // "@Name|text" shows a speaker tag; plain lines are narration. Typed out letter by letter with a blip.
    static readonly string[] FirstEntry =
    {
        "The door closes behind you. It makes no sound.",
        "@Merimna|...Home.",
        "@Merimna|That's what I call it. Home. A word I keep saying so the walls will believe it.",
        "The lamp hums. It has been humming since before you arrived.",
        "@Merimna|Did I leave the lamp on? I always leave the lamp on.",
        "@Merimna|No. I'm sure I turned it off. I'm sure I'm sure I'm sure.",
        "Something in the room is slightly wrong. You can't tell what. You check everything twice.",
        "@Merimna|It's the foxes. No. It's the water. No.",
        "@Merimna|It's not anything. That's what makes it so loud.",
        "@Merimna|My heart is doing that thing again. Counting. It always loses count at the same number.",
        "Outside, something waits patiently. It has never once been in a hurry.",
        "@Merimna|If I stay very still, maybe tomorrow won't notice me.",
        "@Merimna|...Maybe I should write it down. Writing it down makes it smaller. Or it makes it real. I can never remember which.",
        "There are things in this room you are not ready to think about yet.",
        "@Merimna|It's okay. I'm here. I'm always here. That's the problem, and the only comfort.",
        "You are safe. You don't feel safe. Both of these are true.",
    };

    static readonly string[][] ReturnEntry =
    {
        new[] { "@Merimna|Back again.", "The room is exactly as you left it. Almost." },
        new[] { "@Merimna|Okay. Okay. Just breathe. In for four. Out for... I forget the number." },
        new[] { "The lamp hums. You are sure it hummed a different note last time." },
    };

    static readonly string[] ExitLines =
    {
        "Outside. The sky is much too big.",
        "@Merimna|Everything out here is so loud, and so quiet.",
        "The door lets you go, but only just.",
    };

    static readonly string[] Dreams =
    {
        "You dreamed of a staircase that ended in another staircase.",
        "You dreamed the whales were counting you. They lost track at four.",
        "You dreamed of a kitchen where all the spoons were quietly humming.",
        "You dreamed the house was walking. You were its shoes.",
    };

    public static void Build(WorldGenerator world, Transform parent, Vector3 houseBase, Vector3 roomOrigin)
    {
        Rect room = new Rect(roomOrigin.x - RoomW / 2f, roomOrigin.y - RoomH / 2f, RoomW, RoomH);
        Rect outside = world.WorldBounds;
        Vector3 outsideSpot = houseBase + new Vector3(0f, -1.8f, 0f);
        Vector3 insideSpot = roomOrigin + new Vector3(0f, -3.2f, 0f);

        // ---------------------------------------------------------- exterior
        var house = new GameObject("House");
        house.transform.SetParent(parent, false);
        house.transform.position = houseBase;
        house.AddComponent<SpriteRenderer>().sprite = PixelArt.House();
        var body = house.AddComponent<BoxCollider2D>();
        body.size = new Vector2(5.6f, 3.2f);
        body.offset = new Vector2(0f, 1.6f);

        var door = new GameObject("House Door");
        door.transform.SetParent(house.transform, false);
        door.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        bool visited = false;
        Interactable.Create(door, "Press E to go inside", 1.5f, pc => Teleport(pc, insideSpot, room, true, () =>
        {
            // First time: the full scene. Afterwards: a short muttered line.
            string[] script = visited ? ReturnEntry[Random.Range(0, ReturnEntry.Length)] : FirstEntry;
            visited = true;
            GameUI.Ensure().ShowDialogue(script);
        }));

        // ---------------------------------------------------------- interior
        var roomGo = new GameObject("House Interior");
        roomGo.transform.SetParent(parent, false);
        roomGo.transform.position = roomOrigin;
        var floor = roomGo.AddComponent<SpriteRenderer>();
        floor.sprite = PixelArt.Room();
        floor.sortingOrder = -5;

        Wall(roomGo.transform, roomOrigin + new Vector3(0f, -6.5f), new Vector2(26f, 1f));
        Wall(roomGo.transform, roomOrigin + new Vector3(-11.5f, 0f), new Vector2(1f, 14f));
        Wall(roomGo.transform, roomOrigin + new Vector3(11.5f, 0f), new Vector2(1f, 14f));
        Wall(roomGo.transform, roomOrigin + new Vector3(0f, 4.7f), new Vector2(26f, 3f));   // back wall

        // exit (door mat)
        var exit = new GameObject("Exit Mat");
        exit.transform.SetParent(roomGo.transform, false);
        exit.transform.position = roomOrigin + new Vector3(0f, -5f, 0f);
        Interactable.Create(exit, "Press E to go outside", 1.7f, pc => Teleport(pc, outsideSpot, outside, false, () =>
            GameUI.Ensure().ShowDialogue(new[] { ExitLines[Random.Range(0, ExitLines.Length)] })));

        // darkness overlay + lamp glow
        var dark = new GameObject("Room Darkness");
        dark.transform.SetParent(roomGo.transform, false);
        dark.transform.position = roomOrigin;
        dark.transform.localScale = new Vector3(RoomW, RoomH, 1f);
        var darkSr = dark.AddComponent<SpriteRenderer>();
        darkSr.sprite = PixelArt.Solid();
        darkSr.color = new Color(0f, 0f, 0f, 0.12f);
        darkSr.sortingOrder = 40;

        // lamp
        var lamp = Place(roomGo.transform, "Lamp", PixelArt.Lamp(), roomOrigin + new Vector3(-3.4f, 1.8f), new Vector2(0.5f, 0.3f), new Vector2(0f, 0.15f));
        var glowGo = new GameObject("Lamp Glow");
        glowGo.transform.SetParent(lamp.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var glow = glowGo.AddComponent<SpriteRenderer>();
        glow.sprite = PixelArt.Glow();
        glow.color = new Color(1f, 0.85f, 0.5f, 0.45f);
        glow.sortingOrder = 41;
        bool lampOn = true;
        var lampIt = Interactable.Create(lamp, "", 1.8f, pc =>
        {
            lampOn = !lampOn;
            SoundFx.Play("lamp");
            MusicManager.SetLamp(lampOn);
            glow.gameObject.SetActive(lampOn);
            darkSr.color = new Color(0f, 0f, 0f, lampOn ? 0.12f : 0.62f);
        });
        lampIt.promptGetter = () => lampOn ? "Press E to turn the lamp off" : "Press E to turn the lamp on";

        // bed
        var bed = Place(roomGo.transform, "Bed", PixelArt.Bed(), roomOrigin + new Vector3(-6.5f, 1.0f), new Vector2(2.4f, 0.9f), new Vector2(0f, 0.55f));
        Interactable.Create(bed, "Press E to sleep", 2.2f, pc =>
        {
            var ui = GameUI.Ensure();
            ui.ShowChoice("Sleep in the bed?", () =>
                ui.FadeTransition(
                    () => { pc.Health.Heal(pc.Health.maxHealth); SoundFx.Play("sleep"); },
                    () => ui.ShowDialogue(new[] { Dreams[Random.Range(0, Dreams.Length)], "You wake up feeling rested." }),
                    1.5f, "Zzz..."));
        });

        // desk + journal
        var desk = Place(roomGo.transform, "Desk", PixelArt.DeskWithJournal(), roomOrigin + new Vector3(4.5f, 1.6f), new Vector2(2.6f, 0.9f), new Vector2(0f, 0.5f));
        Interactable.Create(desk, "Press E to open the journal", 2.2f, pc =>
            GameUI.Ensure().ShowJournal(JournalPages));

        // table + knife
        var table = Place(roomGo.transform, "Table", PixelArt.TableWithKnife(), roomOrigin + new Vector3(8.3f, -1.6f), new Vector2(2f, 0.7f), new Vector2(0f, 0.4f));
        Interactable.Create(table, "Press E to look at the knife", 2f, pc => KnifeSequence(pc));

        // plant
        int watered = 0;
        var plant = Place(roomGo.transform, "Plant", PixelArt.Plant(), roomOrigin + new Vector3(-9.6f, -1.8f), new Vector2(0.8f, 0.5f), new Vector2(0f, 0.3f));
        Interactable.Create(plant, "Press E to water the plant", 1.8f, pc =>
        {
            watered++;
            SoundFx.Play("drip");
            plant.transform.localScale = Vector3.one * (1f + 0.07f * Mathf.Min(watered, 5));
            string line = watered == 1 ? "You water the plant. Its leaves tremble, just a little."
                        : watered < 5 ? "The plant has grown. It leans toward you, like it's listening."
                        : "The plant is much too big now. It seems to know your name.";
            GameUI.Ensure().ShowDialogue(new[] { line });
        });
    }

    // ----------------------------------------------------------------- helpers

    static void KnifeSequence(PlayerCombat pc)
    {
        var ui = GameUI.Ensure();
        SoundFx.Play("tink");
        ui.ShowDialogue(new[] { "A knife rests on the table. It looks far too clean." }, () =>
            ui.ShowChoice("Pick it up?",
                () =>
                {
                    int lost = pc.Health.HurtSelf(2);
                    if (lost > 0) ui.ShowDialogue(new[] { "...", "It hurts.", "You put it back." });
                    else ui.ShowDialogue(new[] { "You can't bring yourself to." });
                },
                () => ui.ShowDialogue(new[] { "You leave it where it is." })));
    }

    static void Teleport(PlayerCombat pc, Vector3 pos, Rect bounds, bool inside, System.Action after)
    {
        SoundFx.Play("door_open");
        GameUI.Ensure().FadeTransition(() =>
        {
            SoundFx.Play("door_close");
            Vector3 p = new Vector3(pos.x, pos.y, pc.transform.position.z);
            pc.transform.position = p;
            var rb = pc.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = p; GameInput.SetVelocity(rb, Vector2.zero); }
            pc.Health.InHouse = inside;

            Camera cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<BoundedCameraFollow>() : null;
            if (follow != null) { follow.worldBounds = bounds; follow.Snap(p); }
        }, after);
    }

    static GameObject Place(Transform parent, string objName, Sprite sprite, Vector3 pos, Vector2 colSize, Vector2 colOffset)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>().sprite = sprite;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = colSize;
        box.offset = colOffset;
        return go;
    }

    static void Wall(Transform parent, Vector3 pos, Vector2 size)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = size;
    }
}
