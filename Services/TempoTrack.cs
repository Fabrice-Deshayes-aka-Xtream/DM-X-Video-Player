using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DMXVideoPlayer.Services
{
    public class TempoEvent
    {
        public double Bpm { get; set; }
        public double Ppq { get; set; }
        public double TimeInSeconds { get; set; }
        // Bpm adjusted by the time signature's beat factor (e.g. half BPM in 2/2, since the
        // felt beat is the half note rather than the quarter note the raw Bpm is expressed in).
        public double EffectiveBpm { get; set; }
    }

    public class TimeSignatureEvent
    {
        public int Numerator { get; set; }
        public int Denominator { get; set; }
        public double Ppq { get; set; }
        public double TimeInSeconds { get; set; }
    }

    public class TempoTrack
    {
        private List<TempoEvent> _tempoEvents = new List<TempoEvent>();
        private List<double> _beatTimes = new List<double>();
        private List<TimeSignatureEvent> _timeSignatureEvents = new List<TimeSignatureEvent>();
        // Bar/beat number (1-based) associated with each entry of _beatTimes (same index).
        private List<int> _barNumbers = new List<int>();
        private List<int> _beatNumbers = new List<int>();
        private const double PPQ_PER_QUARTER = 480.0;

        public bool IsLoaded => _tempoEvents.Count > 0;

        public double GetBpmAtTime(double seconds)
        {
            if (_tempoEvents.Count == 0)
                return 120.0; // Default BPM

            if (seconds <= 0)
                return _tempoEvents[0].EffectiveBpm;

            for (int i = _tempoEvents.Count - 1; i >= 0; i--)
            {
                if (_tempoEvents[i].TimeInSeconds <= seconds)
                {
                    return _tempoEvents[i].EffectiveBpm;
                }
            }

            return _tempoEvents[0].EffectiveBpm;
        }

        /// <summary>
        /// Returns the time signature (numerator/denominator) in effect at the given time.
        /// Defaults to 4/4 when no MSignatureTrackEvent information is available.
        /// </summary>
        public (int Numerator, int Denominator) GetTimeSignatureAtTime(double seconds)
        {
            if (_timeSignatureEvents.Count == 0)
                return (4, 4);

            if (seconds <= 0)
                return (_timeSignatureEvents[0].Numerator, _timeSignatureEvents[0].Denominator);

            for (int i = _timeSignatureEvents.Count - 1; i >= 0; i--)
            {
                if (_timeSignatureEvents[i].TimeInSeconds <= seconds)
                {
                    return (_timeSignatureEvents[i].Numerator, _timeSignatureEvents[i].Denominator);
                }
            }

            return (_timeSignatureEvents[0].Numerator, _timeSignatureEvents[0].Denominator);
        }

        /// <summary>
        /// Returns the (bar, beat) position, both 1-based, in effect at the given time.
        /// Example: (1, 1) is the first beat of the first bar, (2, 1) the first beat of the
        /// second bar. The number of beats per bar follows the raw numerator of the time
        /// signature in effect for that bar (e.g. 2/2 => 2 beats/bar, 4/4 => 4 beats/bar).
        /// Defaults to (1, 1) when no beat information is available.
        /// </summary>
        public (int Bar, int Beat) GetBarBeatAtTime(double seconds)
        {
            if (_beatTimes.Count == 0)
                return (1, 1);

            if (seconds <= _beatTimes[0])
                return (_barNumbers[0], _beatNumbers[0]);

            for (int i = _beatTimes.Count - 1; i >= 0; i--)
            {
                if (_beatTimes[i] <= seconds)
                {
                    return (_barNumbers[i], _beatNumbers[i]);
                }
            }

            return (_barNumbers[0], _beatNumbers[0]);
        }

        /// <summary>
        /// Finds the exact time of the nearest beat (past or future) relative to the given time
        /// </summary>
        public double GetNearestBeatTime(double currentTimeInSeconds)
        {
            if (_beatTimes.Count == 0)
                return -1.0;

            // Binary search for the nearest beat
            int index = _beatTimes.BinarySearch(currentTimeInSeconds);

            if (index >= 0)
            {
                // Exact time of a found beat
                return _beatTimes[index];
            }
            else
            {
                // BinarySearch returns ~index of the next larger element
                int nextIndex = ~index;

                if (nextIndex == 0)
                {
                    // Before the first beat
                    return _beatTimes[0];
                }
                else if (nextIndex >= _beatTimes.Count)
                {
                    // After the last beat
                    return _beatTimes[_beatTimes.Count - 1];
                }
                else
                {
                    // Between two beats - pick the closest one
                    double prevBeat = _beatTimes[nextIndex - 1];
                    double nextBeat = _beatTimes[nextIndex];

                    double distToPrev = currentTimeInSeconds - prevBeat;
                    double distToNext = nextBeat - currentTimeInSeconds;

                    return (distToPrev <= distToNext) ? prevBeat : nextBeat;
                }
            }
        }

        /// <summary>
        /// Finds the exact time of the next upcoming beat
        /// </summary>
        public double GetNextBeatTime(double currentTimeInSeconds)
        {
            if (_beatTimes.Count == 0)
                return -1.0;

            for (int i = 0; i < _beatTimes.Count; i++)
            {
                if (_beatTimes[i] > currentTimeInSeconds)
                {
                    return _beatTimes[i];
                }
            }

            return -1.0; // No more beats after this time
        }

        public static TempoTrack? LoadFromFile(string smtFilePath)
        {
            if (!File.Exists(smtFilePath))
            {
                return null;
            }

            try
            {
                var tempoTrack = new TempoTrack();
                tempoTrack.ParseSmtFile(smtFilePath);
                return tempoTrack.IsLoaded ? tempoTrack : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tempo track: {ex.Message}");
                return null;
            }
        }

        private void ParseSmtFile(string filePath)
        {
            var doc = XDocument.Load(filePath);

            var tempoEvents = doc.Descendants("obj")
                .Where(obj => obj.Attribute("class")?.Value == "MTempoEvent")
                .Select(obj =>
                {
                    var bpmElement = obj.Descendants("float")
                        .FirstOrDefault(f => f.Attribute("name")?.Value == "BPM");
                    var ppqElement = obj.Descendants("float")
                        .FirstOrDefault(f => f.Attribute("name")?.Value == "PPQ");

                    if (bpmElement != null && ppqElement != null)
                    {
                        return new TempoEvent
                        {
                            Bpm = double.Parse(bpmElement.Attribute("value")?.Value ?? "120",
                                System.Globalization.CultureInfo.InvariantCulture),
                            Ppq = double.Parse(ppqElement.Attribute("value")?.Value ?? "0",
                                System.Globalization.CultureInfo.InvariantCulture),
                            TimeInSeconds = 0
                        };
                    }
                    return null;
                })
                .Where(e => e != null)
                .Cast<TempoEvent>()
                .OrderBy(e => e.Ppq)
                .ToList();

            if (tempoEvents.Count > 0)
            {
                _tempoEvents = tempoEvents;
                ConvertPpqToSeconds();
                ParseTimeSignatureEvents(doc);
                ComputeEffectiveBpm();
                PrecomputeBeatTimes();
                PrecomputeBarBeats();
            }
        }

        private void ParseTimeSignatureEvents(XDocument doc)
        {
            // MTimeSignatureEvent objects live inside the MSignatureTrackEvent root,
            // under a "SignatureEvent" list. Their "Position" is expressed in the same
            // absolute PPQ unit as MTempoEvent.PPQ.
            var signatureRoot = doc.Descendants("obj")
                .FirstOrDefault(obj => obj.Attribute("class")?.Value == "MSignatureTrackEvent");

            if (signatureRoot == null)
                return;

            var signatureEvents = signatureRoot.Descendants("obj")
                .Where(obj => obj.Attribute("class")?.Value == "MTimeSignatureEvent")
                .Select(obj =>
                {
                    var numeratorElement = obj.Elements("int")
                        .FirstOrDefault(f => f.Attribute("name")?.Value == "Numerator");
                    var denominatorElement = obj.Elements("int")
                        .FirstOrDefault(f => f.Attribute("name")?.Value == "Denominator");
                    var positionElement = obj.Elements("int")
                        .FirstOrDefault(f => f.Attribute("name")?.Value == "Position");

                    if (numeratorElement != null && denominatorElement != null && positionElement != null)
                    {
                        return new TimeSignatureEvent
                        {
                            Numerator = int.Parse(numeratorElement.Attribute("value")?.Value ?? "4",
                                System.Globalization.CultureInfo.InvariantCulture),
                            Denominator = int.Parse(denominatorElement.Attribute("value")?.Value ?? "4",
                                System.Globalization.CultureInfo.InvariantCulture),
                            Ppq = double.Parse(positionElement.Attribute("value")?.Value ?? "0",
                                System.Globalization.CultureInfo.InvariantCulture),
                            TimeInSeconds = 0
                        };
                    }
                    return null;
                })
                .Where(e => e != null)
                .Cast<TimeSignatureEvent>()
                .OrderBy(e => e.Ppq)
                .ToList();

            foreach (var signatureEvent in signatureEvents)
            {
                signatureEvent.TimeInSeconds = ConvertPpqValueToSeconds(signatureEvent.Ppq);
            }

            _timeSignatureEvents = signatureEvents.OrderBy(e => e.TimeInSeconds).ToList();
        }

        /// <summary>
        /// Computes, for each tempo event, the "effective" BPM once the time signature's beat
        /// factor is applied. MTempoEvent.BPM is always expressed in quarter notes per minute,
        /// but the felt beat depends on the time signature denominator:
        ///  - Simple time (denominator d): 1 beat = 4/d quarter notes => factor = d/4.
        ///    Example: 2/2 (cut time) => factor = 0.5 => a raw 224 BPM feels like 112 BPM.
        ///  - Compound time (numerator multiple of 3, greater than 3, e.g. 6/8, 9/8, 12/8, 6/4):
        ///    1 beat = dotted note = 3 of the denominator's note value => factor = (d/4) / 3.
        /// Defaults to factor 1.0 (as if 4/4) when no time signature information is available.
        /// </summary>
        private void ComputeEffectiveBpm()
        {
            foreach (var tempoEvent in _tempoEvents)
            {
                double factor = GetBeatFactorAtPpq(tempoEvent.Ppq);
                tempoEvent.EffectiveBpm = tempoEvent.Bpm * factor;
            }
        }

        private double GetBeatFactorAtPpq(double ppq)
        {
            if (_timeSignatureEvents.Count == 0)
                return 1.0;

            if (ppq <= _timeSignatureEvents[0].Ppq)
                return ComputeBeatFactor(_timeSignatureEvents[0]);

            for (int i = _timeSignatureEvents.Count - 1; i >= 0; i--)
            {
                if (_timeSignatureEvents[i].Ppq <= ppq)
                {
                    return ComputeBeatFactor(_timeSignatureEvents[i]);
                }
            }

            return ComputeBeatFactor(_timeSignatureEvents[0]);
        }

        private static double ComputeBeatFactor(TimeSignatureEvent signature)
        {
            double factor = signature.Denominator / 4.0;

            return factor;
        }

        /// <summary>
        /// Converts an arbitrary absolute PPQ value into seconds, using the already-loaded
        /// tempo events as reference points (same logic as ConvertPpqToSeconds, generalized).
        /// </summary>
        private double ConvertPpqValueToSeconds(double targetPpq)
        {
            if (_tempoEvents.Count == 0)
                return 0.0;

            if (targetPpq <= _tempoEvents[0].Ppq)
                return _tempoEvents[0].TimeInSeconds;

            for (int i = 0; i < _tempoEvents.Count; i++)
            {
                var current = _tempoEvents[i];
                var next = (i + 1 < _tempoEvents.Count) ? _tempoEvents[i + 1] : null;

                if (next == null || targetPpq <= next.Ppq)
                {
                    double ppqDelta = targetPpq - current.Ppq;
                    double quarterNotes = ppqDelta / PPQ_PER_QUARTER;
                    double secondsPerQuarter = 60.0 / current.Bpm;
                    return current.TimeInSeconds + quarterNotes * secondsPerQuarter;
                }
            }

            return _tempoEvents[_tempoEvents.Count - 1].TimeInSeconds;
        }

        private void ConvertPpqToSeconds()
        {
            if (_tempoEvents.Count == 0)
                return;

            double currentTimeInSeconds = 0.0;
            _tempoEvents[0].TimeInSeconds = 0.0;

            for (int i = 1; i < _tempoEvents.Count; i++)
            {
                var previousEvent = _tempoEvents[i - 1];
                var currentEvent = _tempoEvents[i];

                double ppqDelta = currentEvent.Ppq - previousEvent.Ppq;
                double quarterNotes = ppqDelta / PPQ_PER_QUARTER;
                double secondsPerQuarter = 60.0 / previousEvent.Bpm;
                double timeDelta = quarterNotes * secondsPerQuarter;

                currentTimeInSeconds += timeDelta;
                currentEvent.TimeInSeconds = currentTimeInSeconds;
            }
        }

        private void PrecomputeBeatTimes()
        {
            _beatTimes.Clear();

            if (_tempoEvents.Count == 0)
                return;

            // Start at the first tempo event
            var firstEvent = _tempoEvents[0];
            double currentBeatTime = firstEvent.TimeInSeconds;
            _beatTimes.Add(currentBeatTime);

            // Generate beats continuously over 5 minutes (or until the end)
            double maxTime = firstEvent.TimeInSeconds + 300.0; // 5 minutes max
            int currentEventIndex = 0;

            while (currentBeatTime < maxTime && currentEventIndex < _tempoEvents.Count)
            {
                var currentEvent = _tempoEvents[currentEventIndex];
                double secondsPerBeat = 60.0 / currentEvent.EffectiveBpm;

                // Compute the next beat
                currentBeatTime += secondsPerBeat;

                // If we've passed the next tempo event, move to the next one
                while (currentEventIndex + 1 < _tempoEvents.Count &&
                       currentBeatTime >= _tempoEvents[currentEventIndex + 1].TimeInSeconds)
                {
                    currentEventIndex++;
                    // Recalculating secondsPerBeat with the new BPM will happen on the next iteration
                }

                // Add the beat if we haven't exceeded the time limit
                if (currentBeatTime < maxTime)
                {
                    _beatTimes.Add(currentBeatTime);
                }
            }
        }

        /// <summary>
        /// Computes, for each entry in _beatTimes, the corresponding (bar, beat) position.
        /// A new bar starts once the number of beats of the current bar (given by the raw
        /// numerator of the time signature in effect when that bar starts) has been reached.
        /// </summary>
        private void PrecomputeBarBeats()
        {
            _barNumbers.Clear();
            _beatNumbers.Clear();

            if (_beatTimes.Count == 0)
                return;

            int currentBar = 1;
            int currentBeatInBar = 1;
            int beatsPerBar = GetTimeSignatureAtTime(_beatTimes[0]).Numerator;
            if (beatsPerBar <= 0)
                beatsPerBar = 4;

            for (int i = 0; i < _beatTimes.Count; i++)
            {
                _barNumbers.Add(currentBar);
                _beatNumbers.Add(currentBeatInBar);

                currentBeatInBar++;
                if (currentBeatInBar > beatsPerBar)
                {
                    currentBeatInBar = 1;
                    currentBar++;

                    // Re-evaluate the beats-per-bar count for the new bar, in case a time
                    // signature change occurs exactly at this bar boundary.
                    double nextBarStartTime = (i + 1 < _beatTimes.Count) ? _beatTimes[i + 1] : _beatTimes[i];
                    beatsPerBar = GetTimeSignatureAtTime(nextBarStartTime).Numerator;
                    if (beatsPerBar <= 0)
                        beatsPerBar = 4;
                }
            }
        }
    }
}
