# Virtual Projector

A hacky virtual video projector solution in Unity based on spot light cookies.

VirtualProjector depends on a high light cookie atlas resolution. Set it to the max: for URP it's UrpAsset-> Lighting -> Additional Lights -> Cookie Atlas Resolution, and for HDRP it's HDRPAsset -> Lighting -> Cookies -> 2D Atlas Size. Yes, the same setting is named differently in the two render pipelines. Just bravo.

Developed in Unity 6.2.

## Known issues

- Low resolution artifact jitter when chaning lens shifting. The light cookie atlas freaks out when chaning light cookie resolution on every frame.
  - Workaround: If it looks terrible after chaning lens shift, then disable and enable the game object and it should update.
- Birp light cookie looks wrong. Note that BiRP light cookies only support grayscale, which it reads from the alpha channel. Perhaps it's time to abandon BiRP.

## Why it's a hack

Unity spot lights use a build-in mask shader that crops the cookie texture to a circle. If we want to render a rectangle we have to fit it inside that circle. The spot cone is always on axis, so to simulate a lens shift (off axis projection) we have to add padding in on both sides no matter which side we shift to. It's a waste of pixels, but this seems to be the dirty hack everyone does.

Note that because both URP and HDRP render all light cookies into an texture atlas internally, there is a limit to how many projections you can fit inside this atlas. When you exceed the limit, Unity will downscale the cookie textures to make them fit and you will see the resolution of your virtual projections. The max atlas size is 4096 in URP and 16384 in HDRP. Because of the spot light circle cropping a 1920x1080 image will need a 2203x2203 area. This means you can only render a single 1080p (full HD) projection in URP. Very sucky, yes.

## Author

Carl Emil Carlsen [cec.dk](https://cec.dk)

## License

Standard MIT license.
