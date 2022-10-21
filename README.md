# Graph View Player 
![screenshot](.github/example.png)

Graph View Player is a _deeply_ refactored version of GraphViewEditor from Unity. Most features have been removed. All that remains are Nodes, Edges, and Ports. No stackable nodes, no collapsible titles, no selection undo/redo, etc.

The two main goals of this project were to make a node editor that:
- Would run in the Unity player
- Would allow for an asynchronous storage backend rather than default to using ScriptableObjects

So far so good! The code should be functional and I'll be improving here and there as I make use of the code for a cross platform dialogue system I'm building.

What follows is an example subclass of GraphView that will allow you to play arround. Just be sure when you add the GraphView to a UI document, you make sure to give it width/height or flex-grow so that it's visible:

```
    public class Testing : GraphView
    {
        protected override void ExecuteCopy() { Debug.Log("OnCopy"); }

        protected override void ExecuteCut() { Debug.Log("OnCut"); }

        protected override void ExecutePaste() { Debug.Log("OnPaste"); }

        protected override void ExecuteDuplicate() { Debug.Log("OnDuplicate"); }

        protected override void ExecuteDelete() { Debug.Log("OnDelete"); }

        protected override void ExecuteUndo() { Debug.Log("OnUndo"); }

        protected override void ExecuteRedo() { Debug.Log("OnRedo"); }

        protected override void ExecuteEdgeCreate(BaseEdge edge)
        {
            Debug.Log("Edge created");
            AddElement(edge);
        }

        protected override void ExecuteEdgeDelete(BaseEdge edge)
        {
            Debug.Log("Edge deleted");
            RemoveElement(edge);
        }

        protected override void OnNodeMoved(BaseNode element) { }

        protected override void OnViewportChanged() { }

        public new class UxmlFactory : UxmlFactory<Testing, UxmlTraits>
        {
        }
    }
```