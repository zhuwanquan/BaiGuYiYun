using System;
using System.Collections.Generic;

namespace BaiguVN
{
    [Serializable]
    public class GameState
    {
        public string currentNodeId;
        public int pageIndex;
        [NonSerialized]
        public HashSet<string> observed = new HashSet<string>();
        public List<HistoryEntry> history = new List<HistoryEntry>();
        public VisualSnapshot visuals = new VisualSnapshot();
        public List<string> completedChapters = new List<string>();
        public bool mainCompleted;
        public string routeId;
        public string routeKind;
        public int routeRevision;
        public bool runCompleted;
        public string resultId;
        public VNRunSnapshot checkpoint;

        public GameState Clone()
        {
            GameState copy = FromRunSnapshot(ToRunSnapshot());
            copy.checkpoint = CloneRunSnapshot(checkpoint);
            return copy;
        }

        public VNSnapshot ToSnapshot(string contentVersion)
        {
            VNSnapshot snapshot = new VNSnapshot
            {
                contentVersion = contentVersion,
                routeCheckpoint = CloneRunSnapshot(checkpoint)
            };
            CopyTo(snapshot);
            return snapshot;
        }

        public VNRunSnapshot ToRunSnapshot()
        {
            VNRunSnapshot snapshot = new VNRunSnapshot();
            CopyTo(snapshot);
            return snapshot;
        }

        public static GameState FromSnapshot(VNSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            GameState state = FromRunSnapshot(snapshot);
            state.checkpoint = CloneRunSnapshot(snapshot.routeCheckpoint);
            return state;
        }

        public static GameState FromRunSnapshot(VNRunSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new GameState
            {
                currentNodeId = snapshot.currentNodeId,
                pageIndex = snapshot.pageIndex,
                observed = snapshot.observed != null
                    ? new HashSet<string>(snapshot.observed) : new HashSet<string>(),
                history = CloneHistory(snapshot.history),
                visuals = CloneVisualSnapshot(snapshot.visuals),
                completedChapters = CloneStrings(snapshot.completedChapters),
                mainCompleted = snapshot.mainCompleted,
                routeId = snapshot.routeId,
                routeKind = snapshot.routeKind,
                routeRevision = snapshot.routeRevision,
                runCompleted = snapshot.runCompleted,
                resultId = snapshot.resultId
            };
        }

        public static VNRunSnapshot CloneRunSnapshot(VNRunSnapshot source)
        {
            if (source == null) return null;
            // Always construct the base type, even when source is a VNSnapshot.
            // Checkpoints cannot acquire another checkpoint through this copy.
            return new VNRunSnapshot
            {
                currentNodeId = source.currentNodeId,
                pageIndex = source.pageIndex,
                observed = CloneStrings(source.observed),
                history = CloneHistory(source.history),
                visuals = CloneVisualSnapshot(source.visuals),
                completedChapters = CloneStrings(source.completedChapters),
                mainCompleted = source.mainCompleted,
                routeId = source.routeId,
                routeKind = source.routeKind,
                routeRevision = source.routeRevision,
                runCompleted = source.runCompleted,
                resultId = source.resultId
            };
        }

        public static VisualSnapshot CloneVisualSnapshot(VisualSnapshot source)
        {
            if (source == null) return new VisualSnapshot();
            VisualSnapshot copy = new VisualSnapshot
            {
                backgroundId = source.backgroundId,
                cgId = source.cgId,
                leftPortraitId = source.leftPortraitId,
                centerPortraitId = source.centerPortraitId,
                rightPortraitId = source.rightPortraitId,
                focusSlot = source.focusSlot,
                bgmId = source.bgmId,
                ambienceId = source.ambienceId
            };
            if (source.props != null)
            {
                foreach (VNPropState prop in source.props)
                {
                    if (prop == null) continue;
                    copy.props.Add(new VNPropState
                    {
                        instanceId = prop.instanceId,
                        assetId = prop.assetId,
                        x = prop.x,
                        y = prop.y,
                        scale = prop.scale
                    });
                }
            }
            return copy;
        }

        private void CopyTo(VNRunSnapshot snapshot)
        {
            snapshot.currentNodeId = currentNodeId;
            snapshot.pageIndex = pageIndex;
            snapshot.observed = observed != null
                ? new List<string>(observed) : new List<string>();
            snapshot.history = CloneHistory(history);
            snapshot.visuals = CloneVisualSnapshot(visuals);
            snapshot.completedChapters = CloneStrings(completedChapters);
            snapshot.mainCompleted = mainCompleted;
            snapshot.routeId = routeId;
            snapshot.routeKind = routeKind;
            snapshot.routeRevision = routeRevision;
            snapshot.runCompleted = runCompleted;
            snapshot.resultId = resultId;
        }

        private static List<string> CloneStrings(List<string> source)
        {
            return source != null ? new List<string>(source) : new List<string>();
        }

        private static List<HistoryEntry> CloneHistory(List<HistoryEntry> source)
        {
            List<HistoryEntry> copy = new List<HistoryEntry>();
            if (source == null) return copy;
            foreach (HistoryEntry entry in source)
            {
                if (entry == null) continue;
                copy.Add(new HistoryEntry
                {
                    nodeId = entry.nodeId,
                    speaker = entry.speaker,
                    text = entry.text
                });
            }
            return copy;
        }
    }
}
