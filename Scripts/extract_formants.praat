# extract_formants.praat
# Usage: praat --run extract_formants.praat <input.wav> <output.csv>
#
# Matches our LPC settings where possible:
#   - Max formant: 5500 Hz
#   - 5 formants
#   - Pre-emphasis from 50 Hz
#   - Window length: 0.025s (25ms)

form Extract Formants
    sentence Wav_file input.wav
    sentence Output_file output.csv
endform

sound = Read from file: wav_file$

selectObject: sound
formant = To Formant (burg): 0.01, 5, 5500, 0.025, 50

deleteFile: output_file$
appendFileLine: output_file$, "time_s,F1,F2,F3,F4,F5"

nFrames = Get number of frames
for i from 1 to nFrames
    t = Get time from frame number: i
    f1 = Get value at time: 1, t, "hertz", "Linear"
    f2 = Get value at time: 2, t, "hertz", "Linear"
    f3 = Get value at time: 3, t, "hertz", "Linear"
    f4 = Get value at time: 4, t, "hertz", "Linear"
    f5 = Get value at time: 5, t, "hertz", "Linear"
    appendFileLine: output_file$, "'t:4','f1:1','f2:1','f3:1','f4:1','f5:1'"
endfor

removeObject: sound, formant
