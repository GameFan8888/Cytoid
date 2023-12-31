﻿using System.Linq.Expressions;
using UnityEngine;

public class NoteRenderer
{
    public Note Note { get; }
    public Game Game => Note.Game;

    protected CircleCollider2D Collider;

    public NoteRenderer(Note note)
    {
        Note = note;
        Collider = note.gameObject.GetComponent<CircleCollider2D>();
        Collider.enabled = true;
    }

    public virtual void OnLateUpdate()
    {
        Render();
    }

    protected virtual void Render() => Expression.Empty();

    public virtual void OnNoteLoaded() => Expression.Empty();

    public virtual void OnClear(NoteGrade grade) => Expression.Empty();

    public bool DoesCollide(Vector2 pos)
    {
        return Collider.OverlapPoint(pos);
    }

    public CircleCollider2D GetCollider() => Collider;

    public virtual void OnCollect()
    {
        if (Collider != null)
        {
            Collider.enabled = false;
        }
    }
    
    public virtual void Dispose() => Expression.Empty();
    
}