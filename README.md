
# Virtual Projector

A hacky virtual video projector solution in Unity based on spot light cookies.

Developed in Unity 6.2

## Why it's a hack
Unity spot lights use a build in mask shader that crops the cookie texture to a circle. If we want to render a
rectangle we have to fit it inside that circle. The spot cone is always on axis, so to simulate a lens shift (off axis projection) 
we have to add padding in on both sides no matter which side we shift to. It's a waste of pixels, but this seems to be the dirty hack everyone does.

Note that because both URP and HDRP render all light cookies to an texture atlas internally, there is a limit to how many projections you can fit into this atlas. When you add more than there is space for you will be the resolution of your projection degrade. The max atlas size is 4096 in URP and 16384 in HDRP. Because of the spot light circle cropping a 1920x1080 image will need a 2203x2203 area. This means you can only render a single full HD projection in URP. Very sucky, yes.

## BiRP
Note that BiRP light cookies only support grayscale, which it reads from the alpha channel.