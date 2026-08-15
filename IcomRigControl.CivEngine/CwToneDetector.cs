using System;
using System.Collections.Generic;

namespace IcomRigControl.CivEngine;

/// A single keyed segment: the tone was On (or off) for this many seconds.
public readonly record struct CwSegment(bool On, double Seconds);

/// <summary>
/// Turns a stream of audio samples into keyed tone on/off segments for the Morse
/// decoder. It measures energy at the CW pitch with a Goertzel filter over a short
/// sliding window (one measurement every "hop" samples), then decides key-down vs
/// key-up with an automatic-gain threshold that floats between a slowly-tracked
/// noise floor and signal peak — so it adapts to fading (QSB) and level changes
/// without a manual level knob. Hysteresis (separate on/off thresholds) stops it
/// chattering on a marginal signal.
///
/// Stateful and streaming: feed it audio as it arrives from IAudioCapture (any
/// chunk size); it buffers the remainder between calls, so a dit split across two
/// capture buffers is still measured as one dit. Call Flush at the end to emit the
/// final in-progress segment. See CLAUDE.md CW decode.
/// </summary>
public sealed class CwToneDetector
{
    private readonly double _pitchHz;
    private readonly int _sampleRate;
    private readonly int _window;
    private readonly int _hop;
    private readonly double _hopSeconds;

    private readonly List<float> _buffer = new();
    private int _pos;

    private bool _seeded;
    private double _hi, _lo;   // AGC envelope: tracked peak and floor of the magnitude
    private bool _keyDown;
    private int _runHops;

    // How quickly the peak/floor relax back toward the signal when not being pushed
    // out by a new extreme. Per hop; small = slow (rides through QSB).
    private const double Relax = 0.0008;

    public CwToneDetector(double pitchHz, int sampleRateHz, int? windowSamples = null, int? hopSamples = null)
    {
        _pitchHz = pitchHz;
        _sampleRate = sampleRateHz;
        // ~10 ms window (several cycles of the pitch for a clean Goertzel), 5 ms hop.
        _window = windowSamples ?? Math.Max(64, sampleRateHz / 100);
        _hop = hopSamples ?? Math.Max(32, _window / 2);
        _hopSeconds = _hop / (double)_sampleRate;
    }

    /// Feeds a chunk of float audio (-1..1). Returns any segments completed by it.
    public IEnumerable<CwSegment> Feed(float[] samples)
    {
        _buffer.AddRange(samples);
        var completed = new List<CwSegment>();

        while (_pos + _window <= _buffer.Count)
        {
            double mag = Math.Sqrt(GoertzelPower(_buffer, _pos, _window, _pitchHz, _sampleRate));
            _pos += _hop;

            bool down = Decide(mag);
            if (down == _keyDown)
            {
                _runHops++;
            }
            else
            {
                completed.Add(new CwSegment(_keyDown, _runHops * _hopSeconds));
                _keyDown = down;
                _runHops = 1;
            }
        }

        // Drop the samples we've fully hopped past; keep the remainder.
        if (_pos > 0)
        {
            _buffer.RemoveRange(0, _pos);
            _pos = 0;
        }
        return completed;
    }

    /// Emits the final in-progress segment. Call once at end of stream.
    public CwSegment? Flush()
    {
        if (_runHops <= 0) return null;
        var seg = new CwSegment(_keyDown, _runHops * _hopSeconds);
        _runHops = 0;
        return seg;
    }

    private bool Decide(double mag)
    {
        if (!_seeded)
        {
            _hi = _lo = mag;
            _seeded = true;
            return false; // start key-up
        }

        // Track the envelope: snap to a new extreme, otherwise relax slowly toward it.
        if (mag > _hi) _hi = mag; else _hi += Relax * (mag - _hi);
        if (mag < _lo) _lo = mag; else _lo += Relax * (mag - _lo);

        double range = _hi - _lo;
        if (range <= 1e-9) return false; // flat (silence/no signal) -> key-up

        // Hysteresis around the midpoint: need 60% to key down, drop below 40% to key up.
        double onThresh = _lo + 0.60 * range;
        double offThresh = _lo + 0.40 * range;
        if (_keyDown) return mag > offThresh;
        return mag > onThresh;
    }

    private static double GoertzelPower(List<float> x, int start, int count, double freq, int sampleRate)
    {
        double w = 2 * Math.PI * freq / sampleRate;
        double coeff = 2 * Math.Cos(w);
        double s1 = 0, s2 = 0;
        for (int i = 0; i < count; i++)
        {
            double s0 = x[start + i] + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }
        return s1 * s1 + s2 * s2 - coeff * s1 * s2;
    }
}
