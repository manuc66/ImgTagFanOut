# ONNX models

All models run locally, on the CPU. Nothing is sent to a remote service.

## face_detection_yunet_2023mar.onnx

YuNet face detector, from OpenCV Zoo. Used by `OpenCvFaceAnalyzer`.

## face_recognition_sface_2021dec.onnx

SFace face embedder, from OpenCV Zoo. Used by `OpenCvFaceAnalyzer`.

## image_embedding_mobilenetv2.onnx

MobileNetV2 1.0 (224x224), Apache-2.0, from
[`google/mobilenet_v2_1.0_224`](https://huggingface.co/google/mobilenet_v2_1.0_224),
exported by [`onnx-community/mobilenet_v2_1.0_224-ONNX`](https://huggingface.co/onnx-community/mobilenet_v2_1.0_224-ONNX).

The original export only exposes the 1001 ImageNet logits. This copy adds the
1280-value global average pooling tensor as a second output (`features`), which
is a much better image descriptor than the class scores. The classifier weights
are untouched.

- Input: `pixel_values`, float32 `[N, 3, 224, 224]`, RGB, `(value / 255 - 0.5) / 0.5`
- Outputs: `logits` (`[N, 1001]`) and
  `/mobilenet_v2/pooler/GlobalAveragePool_output_0` (float32 `[N, 1280, 1, 1]`)
- SHA-256: `d8373c5c9d3b9333ef7e3a3606310d4ba63d75e7172f757c00ef8ca5bab36ef5`

Preprocessing follows `preprocessor_config.json`: resize the shortest edge to
256 px, center-crop 224x224, rescale by 1/255, then normalise with mean and
standard deviation 0.5.

Used by `OnnxImageEmbedder` for the local tag suggestion engine.
