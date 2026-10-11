using System;
using Godot;
using CustomRhythia.Map;

namespace CustomRhythia.Game;

// Game loop: advances progress, starts the song when gameplay begins,
// corrects drift gently, resolves note hits.
//
// Modifiers:
//   NoFail      - misses never cost HP; the run cannot fail from HP.
//   SuddenDeath - any miss ends the run instantly.
//   AutoPlay    - the runner owns the cursor and moves it smoothly to
//                 each note. Hit registration is time-based, so cursor
//                 smoothing can never cause a miss.
public partial class Runner : Node
{
    [Signal] public delegate void StatsUpdatedEventHandler();
    [Signal] public delegate void AttemptFinishedEventHandler();

    public Attempt Attempt { get; private set; }
    public bool IsPlaying => Attempt != null && Attempt.Playing;

    private AudioStreamPlayer _song;
    private AudioStreamPlayer[] _hitSounds = Array.Empty<AudioStreamPlayer>();
    private int _hitSoundIndex = 0;

    private ulong _lastFrameUs;
    private bool _firstFrame = true;

    private bool _songStarted = false;
    private ulong _songStartedUs = 0;
    private ulong _lastForcedSeekUs = 0;
    private const ulong SongSettleUs = 700000;
    private const ulong ForcedSeekCooldownUs = 1000000;

    private int _scanStart = 0;
    private int _scanEnd = 0;

    private double _cursorX;
    private double _cursorY;

    public double CursorX => _cursorX;
    public double CursorY => _cursorY;

    public void Setup(Attempt attempt, AudioStreamPlayer song, AudioStreamPlayer[] hitSounds)
    {
        Attempt = attempt;
        _song = song;
        _hitSounds = hitSounds ?? Array.Empty<AudioStreamPlayer>();
        _hitSoundIndex = 0;

        _lastFrameUs = Time.GetTicksUsec();
        _firstFrame = true;
        _songStarted = false;
        _songStartedUs = 0;
        _lastForcedSeekUs = 0;

        if (attempt != null && attempt.Map != null && attempt.Map.Notes != null)
        {
            var notes = attempt.Map.Notes;
            for (int i = 0; i < notes.Length; i++)
            {
                var n = notes[i];
                n.HitOffset = 0f;

                if (attempt.StartFrom > 0 && n.Millisecond < attempt.StartFrom - 200)
                {
                    n.LastResult = HitResult.Hit;
                }
                else
                {
                    n.LastResult = HitResult.None;
                }
            }
        }

        _scanStart = 0;
        _scanEnd = 0;
        if (attempt != null && attempt.Map != null && attempt.Map.Notes != null)
        {
            for (int i = 0; i < attempt.Map.Notes.Length; i++)
            {
                if (attempt.Map.Notes[i].Millisecond >= attempt.StartFrom - 200)
                {
                    _scanStart = i;
                    _scanEnd = i;
                    break;
                }
            }
        }

        if (attempt != null && attempt.AutoPlay)
        {
            var notes = attempt.Map.Notes;
            int i = _scanStart;
            while (i < notes.Length && notes[i].LastResult != HitResult.None) i++;
            if (i < notes.Length)
            {
                _cursorX = notes[i].X;
                _cursorY = notes[i].Y;
            }
        }
    }

    public void NotifyResumed()
    {
        _lastFrameUs = Time.GetTicksUsec();
        _firstFrame = true;
        _songStartedUs = Time.GetTicksUsec();
    }

    public void SetCursor(double x, double y)
    {
        _cursorX = x;
        _cursorY = y;
    }

    public override void _Process(double delta)
    {
        if (Attempt == null || !Attempt.Playing || Attempt.Stopped) return;

        ulong now = Time.GetTicksUsec();
        if (_firstFrame)
        {
            _lastFrameUs = now;
            _firstFrame = false;
            return;
        }

        double dt = (now - _lastFrameUs) / 1000000.0;
        _lastFrameUs = now;
        if (dt > 0.1) dt = 0.1;

        Attempt.Progress = Attempt.Progress + dt * 1000.0 * Attempt.Speed;

        StartSongIfNeeded(now);
        UpdateAudioSync(now);

        if (Attempt.AutoPlay) UpdateAutoPlayCursor(dt);

        ResolveNotes();

        if (Attempt.Progress >= Attempt.Length && !Attempt.Stopped)
        {
            Attempt.Stopped = true;
            Attempt.Playing = false;
            EmitSignal(SignalName.AttemptFinished);
        }
    }

    // AutoPlay: ease the cursor toward the next unresolved note. The
    // approach rate grows as the note gets closer, so the cursor is calm
    // during long gaps and speeds up right before the hit.
    private void UpdateAutoPlayCursor(double dt)
    {
        if (Attempt == null || !Attempt.AutoPlay) return;
        var notes = Attempt.Map.Notes;
        if (notes == null || notes.Length == 0) return;

        int i = _scanStart;
        while (i < notes.Length && notes[i].LastResult != HitResult.None) i++;
        if (i >= notes.Length) return;

        var n = notes[i];
        double remaining = n.Millisecond - Attempt.Progress;

        double rate = 8.0;
        if (remaining < 150.0) rate = 15.0;
        if (remaining < 60.0) rate = 30.0;

        double alpha = 1.0 - Math.Exp(-rate * dt);
        if (alpha > 1.0) alpha = 1.0;
        if (alpha < 0.0) alpha = 0.0;

        _cursorX += (n.X - _cursorX) * alpha;
        _cursorY += (n.Y - _cursorY) * alpha;

        if (Math.Abs(n.X - _cursorX) < 0.001 && Math.Abs(n.Y - _cursorY) < 0.001)
        {
            _cursorX = n.X;
            _cursorY = n.Y;
        }
    }

    private void StartSongIfNeeded(ulong now)
    {
        if (_songStarted) return;
        if (_song == null || _song.Stream == null) return;
        if (Attempt.Progress < Attempt.StartFrom) return;

        if (_song.Playing)
        {
            _song.PitchScale = (float)Attempt.Speed;
        }
        else
        {
            float startSec = (float)(Attempt.StartFrom / 1000.0);
            _song.PitchScale = (float)Attempt.Speed;
            _song.Play(startSec);
        }

        _songStarted = true;
        _songStartedUs = now;
    }

    private void UpdateAudioSync(ulong now)
    {
        if (!_songStarted || _song == null) return;
        if (_song.StreamPaused) return;
        if (!_song.Playing) return;
        if (now - _songStartedUs < SongSettleUs) return;

        double expected = Attempt.Progress;
        double actual = 1000.0 * _song.GetPlaybackPosition();
        double offset = expected - actual;

        if (Math.Abs(offset) > 300.0)
        {
            if (now - _lastForcedSeekUs > ForcedSeekCooldownUs)
            {
                _song.Seek((float)(expected / 1000.0));
                _lastForcedSeekUs = now;
                _songStartedUs = now;
            }
            _song.PitchScale = (float)Attempt.Speed;
            return;
        }

        if (Math.Abs(offset) > 40.0)
        {
            double target = Attempt.Speed + offset / 5000.0;
            double lo = Attempt.Speed * 0.98;
            double hi = Attempt.Speed * 1.02;
            if (target < lo) target = lo;
            if (target > hi) target = hi;
            _song.PitchScale = (float)target;
        }
        else
        {
            _song.PitchScale = (float)Attempt.Speed;
        }
    }

    private void ResolveNotes()
    {
        var notes = Attempt.Map.Notes;
        if (notes == null || notes.Length == 0) return;

        while (_scanStart < notes.Length && notes[_scanStart].LastResult != HitResult.None)
        {
            _scanStart = _scanStart + 1;
        }

        double hitWindow = Constants.HIT_WINDOW * Attempt.Speed;

        if (_scanEnd < _scanStart) _scanEnd = _scanStart;
        double lookahead = hitWindow + 64.0;
        while (_scanEnd < notes.Length && notes[_scanEnd].Millisecond <= Attempt.Progress + lookahead)
        {
            _scanEnd = _scanEnd + 1;
        }

        for (int i = _scanStart; i < _scanEnd; i++)
        {
            Note note = notes[i];
            if (note.LastResult != HitResult.None) continue;

            double dt = Attempt.Progress - note.Millisecond;
            if (dt < 0) continue;

            if (Attempt.AutoPlay)
            {
                RegisterHit(note);
                continue;
            }

            bool cursorInside = note.IsCursorInside((float)_cursorX, (float)_cursorY);

            if (cursorInside)
            {
                RegisterHit(note);
            }
            else if (dt > hitWindow)
            {
                RegisterMiss(note);
            }
        }
    }

    public void Stop()
    {
        if (Attempt == null || Attempt.Stopped) return;
        Attempt.Stopped = true;
        Attempt.Playing = false;
        EmitSignal(SignalName.AttemptFinished);
    }

    public void Skip()
    {
        if (Attempt == null) return;

        if (Attempt.Progress >= Attempt.Length - 2000)
        {
            Stop();
            return;
        }

        var notes = Attempt.Map.Notes;
        double target = Attempt.Progress + 4000;

        for (int i = _scanStart; i < notes.Length; i++)
        {
            Note n = notes[i];
            if (n.LastResult != HitResult.None) continue;
            if (n.Millisecond > Attempt.Progress)
            {
                double candidate = n.Millisecond - 1000.0 * Attempt.Speed;
                double minTarget = Attempt.Progress + 1000;
                target = candidate > minTarget ? candidate : minTarget;
                break;
            }
        }

        Attempt.Progress = target;
        _scanStart = 0;
        _scanEnd = 0;

        if (_song != null && _songStarted && !_song.StreamPaused)
        {
            _song.Seek((float)(Attempt.Progress / 1000.0));
            _songStartedUs = Time.GetTicksUsec();
            _lastForcedSeekUs = _songStartedUs;
        }
    }

    private void RegisterHit(Note note)
    {
        note.LastResult = HitResult.Hit;
        note.HitOffset = (float)(Attempt.Progress - note.Millisecond);

        Attempt.Hits = Attempt.Hits + 1;
        Attempt.Sum = Attempt.Sum + 1;
        Attempt.Combo = Attempt.Combo + 1;
        Attempt.ComboMultiplierProgress = Attempt.ComboMultiplierProgress + 1;

        if (Attempt.Combo > Attempt.MaxCombo) Attempt.MaxCombo = Attempt.Combo;

        if (Attempt.ComboMultiplierProgress >= Attempt.ComboMultiplierIncrement
            && Attempt.ComboMultiplier < 8)
        {
            Attempt.ComboMultiplierProgress = 0;
            Attempt.ComboMultiplier = Attempt.ComboMultiplier + 1;
        }

        double speedFactor = (Attempt.Speed - 1) / 2.5 + 1;
        uint score = (uint)(100 * Attempt.ComboMultiplier * speedFactor);
        Attempt.Score = Attempt.Score + score;

        double healed = Attempt.Health + 1;
        Attempt.Health = healed > Attempt.MaxHealth ? Attempt.MaxHealth : healed;

        PlayHitSound();

        EmitSignal(SignalName.StatsUpdated);
    }

    private void PlayHitSound()
    {
        if (_hitSounds.Length == 0) return;
        if (_hitSounds[0] == null || _hitSounds[0].Stream == null) return;

        var player = _hitSounds[_hitSoundIndex];
        _hitSoundIndex = (_hitSoundIndex + 1) % _hitSounds.Length;
        if (player != null) player.Play();
    }

    private void RegisterMiss(Note note)
    {
        note.LastResult = HitResult.Miss;
        note.HitOffset = -1;

        Attempt.Misses = Attempt.Misses + 1;
        Attempt.Sum = Attempt.Sum + 1;
        Attempt.Combo = 0;
        Attempt.ComboMultiplierProgress = 0;

        uint mult = Attempt.ComboMultiplier;
        if (mult > 1) mult = mult - 1;
        Attempt.ComboMultiplier = mult;

        if (Attempt.SuddenDeath)
        {
            Attempt.Health = 0;
            Attempt.Alive = false;
            Attempt.Qualifies = false;
            Attempt.Playing = false;
            Attempt.Stopped = true;
            EmitSignal(SignalName.StatsUpdated);
            EmitSignal(SignalName.AttemptFinished);
            return;
        }

        if (!Attempt.NoFail)
        {
            double dmg = Attempt.Health - 1;
            Attempt.Health = dmg > 0 ? dmg : 0;

            if (Attempt.Health <= 0 && Attempt.Alive)
            {
                Attempt.Alive = false;
                Attempt.Qualifies = false;
                Attempt.Playing = false;
                Attempt.Stopped = true;
                EmitSignal(SignalName.StatsUpdated);
                EmitSignal(SignalName.AttemptFinished);
                return;
            }
        }

        EmitSignal(SignalName.StatsUpdated);
    }
}
