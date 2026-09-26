import Foundation
import ScreenCaptureKit
import AVFoundation
import CoreMedia

// Audio only. The filter includes the dedicated Chrome application PID, never all apps.
final class AudioOutput: NSObject, SCStreamOutput, SCStreamDelegate, @unchecked Sendable {
    let queue = DispatchQueue(label: "AssetRaider.Audio")
    private var expected: Double?
    private var failed = false
    func fail(_ message: String) {
        if failed { return }; failed = true
        FileHandle.standardError.write(Data((message + "\n").utf8))
        Darwin.exit(1)
    }
    func stream(_ stream: SCStream, didStopWithError error: Error) { fail(error.localizedDescription) }
    func stream(_ stream: SCStream, didOutputSampleBuffer sample: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .audio, sample.isValid, !failed else { return }
        do {
            guard let description = sample.formatDescription,
                  let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(description),
                  let format = AVAudioFormat(streamDescription: asbd),
                  format.sampleRate == 48000, format.channelCount == 2,
                  format.commonFormat == .pcmFormatFloat32 else {
                fail("Unexpected audio format; expected 48 kHz stereo Float32."); return
            }
            let time = CMTimeGetSeconds(sample.presentationTimeStamp)
            let frames = sample.numSamples
            if let previous = expected {
                let gap = Int(((time - previous) * 48000).rounded())
                if gap > 96000 || gap < -480 { fail("Audio timing changed unexpectedly; recording kept as partial."); return }
                if gap > 0 { FileHandle.standardOutput.write(Data(count: gap * 4)) }
            }
            expected = time + Double(frames) / 48000
            try sample.withAudioBufferList { buffers, _ in
                guard let pcm = AVAudioPCMBuffer(pcmFormat: format, bufferListNoCopy: buffers.unsafePointer),
                      let channels = pcm.floatChannelData else { throw NSError(domain: "AssetRaider", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not read audio samples."]) }
                var encoded = [Int16](repeating: 0, count: frames * 2)
                for frame in 0..<frames {
                    for channel in 0..<2 {
                        let value = format.isInterleaved ? channels[0][frame * 2 + channel] : channels[channel][frame]
                        let finite = value.isFinite ? value : 0
                        encoded[frame * 2 + channel] = Int16(max(-32768, min(32767, Int((finite * 32767).rounded())))).littleEndian
                    }
                }
                encoded.withUnsafeBytes { FileHandle.standardOutput.write(Data($0)) }
            }
        } catch { fail(error.localizedDescription) }
    }
}

@main struct AudioCapture {
    static func main() async {
        if CommandLine.arguments.contains("--check") { print("ScreenCaptureKit audio helper: available on macOS 14+"); return }
        guard CommandLine.arguments.count == 2, let pid = Int32(CommandLine.arguments[1]), pid > 0 else {
            FileHandle.standardError.write(Data("Expected the dedicated Chrome process ID.\n".utf8)); Darwin.exit(1)
        }
        let output = AudioOutput()
        do {
            let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: false)
            guard let display = content.displays.first,
                  let application = content.applications.first(where: { $0.processID == pid }) else {
                throw NSError(domain: "AssetRaider", code: 2, userInfo: [NSLocalizedDescriptionKey: "The dedicated Chrome application is not visible to macOS capture. Keep its window open and allow Screen & System Audio Recording for AssetRaider in System Settings, then restart the app."])
            }
            let filter = SCContentFilter(display: display, includingApplications: [application], exceptingWindows: [])
            let config = SCStreamConfiguration()
            config.capturesAudio = true; config.excludesCurrentProcessAudio = true
            config.sampleRate = 48000; config.channelCount = 2
            config.width = 2; config.height = 2; config.minimumFrameInterval = CMTime(value: 1, timescale: 1)
            let stream = SCStream(filter: filter, configuration: config, delegate: output)
            try stream.addStreamOutput(output, type: .audio, sampleHandlerQueue: output.queue)
            try await stream.startCapture()
            FileHandle.standardError.write(Data("READY\n".utf8))
            // EOF also stops capture if the parent app exits. No microphone or video output is requested.
            _ = await Task.detached { readLine() }.value
            try await stream.stopCapture()
            output.queue.sync { }
        } catch { output.fail("macOS audio capture: " + error.localizedDescription + " Allow Screen & System Audio Recording for AssetRaider, then restart it.") }
    }
}
