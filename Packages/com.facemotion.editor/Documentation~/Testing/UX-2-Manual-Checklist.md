# UX-2 Manual Checklist

1. Select an avatar, start Preview, and scrub the timeline ruler repeatedly. Confirm the playhead, inactive inspector time, and preview pose update together.
2. Select one key, scrub elsewhere, and confirm the inspector continues to show that key's fields rather than replacing them with scrub time.
3. Deselect all keys, scrub to several times, and confirm the inspector time follows the playhead.
4. Start playback, then pause and scrub. Confirm preview pose matches the final playhead time in every case.
5. With Preview running, scrub continuously for several seconds. Confirm the preview camera orbit/pan state is retained; it must not reset or visibly recreate the clone.
6. Change the selected avatar while Preview is running. Confirm preview switches to the new avatar once, and then retains its camera state while scrubbing it.
7. Add, move, undo, redo, and delete keys while preview is running. Confirm timeline and preview refresh without IMGUI collection-modified exceptions.
