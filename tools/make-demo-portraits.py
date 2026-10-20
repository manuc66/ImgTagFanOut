"""Builds the demo face set from public domain portraits (Wikimedia Commons).

US government official portraits are in the public domain. Each person is
downloaded in several real shots, cropped around the face, and duplicates (the
same photo cropped differently) are dropped by embedding similarity.

Usage: python make-demo-portraits.py <output-folder>

Requires: pip install opencv-python-headless
Expects the YuNet and SFace models next to this script (see MODELS).
"""

import io
import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

import cv2
import numpy as np

MODELS = "./models"
API = "https://commons.wikimedia.org/w/api.php"

PERSONS = {
    "obama": [
        "President Barack Obama.jpg",
        "President Barack Obama, 2012 portrait crop.jpg",
        "Barack Obama 2009 official portrait.jpg",
    ],
    "bush-jr": [
        "George-W-Bush.jpeg",
        "George W. Bush 2003 (4x5).png",
        "George W. Bush 108th Congressional Portrait.png",
    ],
    "clinton": [
        "Bill Clinton 1999.jpg",
        "Bill Clinton 1986.jpg",
        "Bill Clinton official.jpg",
    ],
    "bush-sr": [
        "Official portrait of President George H W Bush January 1992.jpg",
        "George H. W. Bush official CIA portrait.jpg",
        "Official portrait of Vice President George H. W. Bush, 1985.jpg",
    ],
    "reagan": [
        "Ronald Reagan 1981 presidential portrait.jpg",
        "Ronald Reagan 1985 presidential portrait (cropped).jpg",
        "Official Portrait of President Reagan 1981.jpg",
    ],
    "carter": [
        "Jimmy Carter, official portrait.jpg",
        "Jimmy Carter 1971.jpg",
        "Official Portrait of Jimmy Carter (1977).jpg",
    ],
}

SIZE = 400
MARGIN = 1.5
DUPLICATE = 0.985


def api(titles):
    query = {
        "action": "query",
        "format": "json",
        "prop": "imageinfo",
        "iiprop": "url|extmetadata",
        "iiurlwidth": "900",
        "titles": "|".join("File:" + t for t in titles),
    }
    url = API + "?" + urllib.parse.urlencode(query)
    request = urllib.request.Request(url, headers={"User-Agent": "ImgTagFanOut-demo/1.0 (contact: project maintainer)"})
    for attempt in range(5):
        try:
            with urllib.request.urlopen(request) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            if error.code != 429 or attempt == 4:
                raise
            time.sleep(5 * (attempt + 1))


def download(url):
    request = urllib.request.Request(url, headers={"User-Agent": "ImgTagFanOut-demo/1.0"})
    for attempt in range(5):
        try:
            with urllib.request.urlopen(request) as response:
                return np.frombuffer(response.read(), np.uint8)
        except urllib.error.HTTPError as error:
            if error.code != 429 or attempt == 4:
                raise
            time.sleep(3 * (attempt + 1))


def main():
    output = Path(sys.argv[1])
    detector = cv2.FaceDetectorYN.create(f"{MODELS}/face_detection_yunet_2023mar.onnx", "", (320, 320), 0.6, 0.3, 5000)
    recognizer = cv2.FaceRecognizerSF.create(f"{MODELS}/face_recognition_sface_2021dec.onnx", "")

    manifest = {"source": "Wikimedia Commons, public domain (US government works)", "persons": {}}

    for slug, titles in PERSONS.items():
        info = api(titles).get("query", {}).get("pages", {})
        by_title = {}
        for page in info.values():
            if "imageinfo" not in page:
                continue
            ii = page["imageinfo"][0]
            by_title[page["title"][5:]] = ii

        kept = []
        embeddings = []
        for title in titles:
            ii = by_title.get(title)
            if ii is None:
                print("  absent:", title)
                continue
            try:
                data = download(ii.get("thumburl") or ii["url"])
                image = cv2.imdecode(data, cv2.IMREAD_COLOR)
            except Exception as error:  # noqa: BLE001 - on saute, le reste continue
                print("  echec:", title, error)
                continue
            if image is None:
                continue

            detector.setInputSize((image.shape[1], image.shape[0]))
            _, faces = detector.detect(image)
            if faces is None or len(faces) == 0:
                print("  pas de visage:", title)
                continue

            face = faces[np.argmax(faces[:, 2] * faces[:, 3])]
            aligned = recognizer.alignCrop(image, face)
            embedding = recognizer.feature(aligned)[0]
            embedding = embedding / np.linalg.norm(embedding)

            if any(float(embedding @ other) > DUPLICATE for other in embeddings):
                print("  doublon:", title)
                continue

            embeddings.append(embedding)
            kept.append((title, crop(image, face), ii.get("descriptionurl")))

        time.sleep(2)
        directory = output / slug
        directory.mkdir(parents=True, exist_ok=True)
        files = []
        for index, (title, image, page) in enumerate(kept):
            path = directory / f"photo-{index + 1}.jpg"
            cv2.imwrite(str(path), image, [cv2.IMWRITE_JPEG_QUALITY, 88])
            files.append({"file": str(path.relative_to(output)), "commons": title, "page": page})

        manifest["persons"][slug] = files
        print(f"{slug}: {len(kept)} photo(s)")

    (output / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False))


def crop(image, face):
    x, y, w, h = face[:4]
    side = max(w, h) * MARGIN
    cx, cy = x + w / 2, y + h / 2
    x1, y1 = int(max(0, cx - side / 2)), int(max(0, cy - side / 2))
    x2, y2 = int(min(image.shape[1], cx + side / 2)), int(min(image.shape[0], cy + side / 2))
    return cv2.resize(image[y1:y2, x1:x2], (SIZE, SIZE), interpolation=cv2.INTER_CUBIC)


if __name__ == "__main__":
    main()
